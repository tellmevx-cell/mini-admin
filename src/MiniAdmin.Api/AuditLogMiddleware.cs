using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using MiniAdmin.Application.Contracts.AuditLogs;

internal sealed class AuditLogMiddleware(RequestDelegate next)
{
    private const int MaxStoredBodyLength = 4000;

    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "oldPassword",
        "newPassword",
        "confirmPassword",
        "token",
        "accessToken",
        "refreshToken",
        "authorization",
        "secret",
        "signingKey"
    };

    public async Task InvokeAsync(
        HttpContext context,
        IAuditLogRepository auditLogRepository,
        IAuditEntityChangeCollector auditEntityChangeCollector)
    {
        if (!ShouldAudit(context.Request))
        {
            await next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var requestBody = await ReadSanitizedRequestBodyAsync(context.Request);
        Exception? exception = null;
        auditEntityChangeCollector.Enable();

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            exception = ex;
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            auditEntityChangeCollector.Disable();
            await auditLogRepository.CreateAsync(
                new SaveAuditLogRequest(
                    UserId: GetUserId(context),
                    UserName: GetUserName(context, requestBody),
                    Method: context.Request.Method,
                    Path: context.Request.Path.Value ?? string.Empty,
                    QueryString: NormalizeQueryString(context.Request.QueryString.Value),
                    Module: GetModule(context.Request.Path),
                    Action: GetAction(context.Request),
                    ResourceId: GetResourceId(context.Request.Path),
                    StatusCode: context.Response.StatusCode,
                    IsSuccess: exception is null && context.Response.StatusCode is >= 200 and < 400,
                    ElapsedMilliseconds: stopwatch.ElapsedMilliseconds,
                    IpAddress: context.Connection.RemoteIpAddress?.ToString(),
                    UserAgent: context.Request.Headers.UserAgent.ToString(),
                    RequestBody: requestBody,
                    ErrorMessage: exception?.Message,
                    CreatedAt: DateTimeOffset.UtcNow,
                    EntityChanges: auditEntityChangeCollector.GetChanges()),
                context.RequestAborted);
        }
    }

    private static bool ShouldAudit(HttpRequest request)
    {
        return HttpMethods.IsPost(request.Method) ||
               HttpMethods.IsPut(request.Method) ||
               HttpMethods.IsDelete(request.Method);
    }

    private static async Task<string> ReadSanitizedRequestBodyAsync(HttpRequest request)
    {
        if (request.ContentLength is null or 0)
        {
            return string.Empty;
        }

        if (request.HasFormContentType)
        {
            return "[multipart/form-data omitted]";
        }

        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        return Truncate(SanitizeBody(body));
    }

    private static string SanitizeBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        try
        {
            var node = JsonNode.Parse(body);
            if (node is null)
            {
                return body;
            }

            SanitizeNode(node);
            return node.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = false
            });
        }
        catch (JsonException)
        {
            return body;
        }
    }

    private static void SanitizeNode(JsonNode node)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var key in jsonObject.Select(item => item.Key).ToArray())
            {
                if (SensitiveKeys.Contains(key))
                {
                    jsonObject[key] = "***";
                    continue;
                }

                if (jsonObject[key] is { } child)
                {
                    SanitizeNode(child);
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (var child in jsonArray)
            {
                if (child is not null)
                {
                    SanitizeNode(child);
                }
            }
        }
    }

    private static string Truncate(string value)
    {
        return value.Length <= MaxStoredBodyLength ? value : value[..MaxStoredBodyLength];
    }

    private static string? GetUserId(HttpContext context)
    {
        return context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private static string? GetUserName(HttpContext context, string sanitizedRequestBody)
    {
        return context.User.Identity?.Name ??
               context.User.FindFirstValue(ClaimTypes.Name) ??
               TryGetUserNameFromBody(sanitizedRequestBody);
    }

    private static string? TryGetUserNameFromBody(string sanitizedRequestBody)
    {
        if (string.IsNullOrWhiteSpace(sanitizedRequestBody))
        {
            return null;
        }

        try
        {
            var node = JsonNode.Parse(sanitizedRequestBody);
            return node?["username"]?.GetValue<string>() ??
                   node?["userName"]?.GetValue<string>();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? NormalizeQueryString(string? queryString)
    {
        return string.IsNullOrWhiteSpace(queryString) ? null : queryString;
    }

    private static string GetModule(PathString path)
    {
        var segments = GetSegments(path);
        return segments.Length > 0 ? ToTitleCase(segments[0]) : "Unknown";
    }

    private static string GetAction(HttpRequest request)
    {
        if (request.Path.StartsWithSegments("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            return "Login";
        }

        if (HttpMethods.IsPost(request.Method))
        {
            return "Create";
        }

        if (HttpMethods.IsPut(request.Method))
        {
            return "Update";
        }

        return HttpMethods.IsDelete(request.Method) ? "Delete" : request.Method;
    }

    private static string? GetResourceId(PathString path)
    {
        var lastSegment = GetSegments(path).LastOrDefault();
        return Guid.TryParse(lastSegment, out _) ? lastSegment : null;
    }

    private static string[] GetSegments(PathString path)
    {
        return (path.Value ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string ToTitleCase(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : char.ToUpperInvariant(value[0]) + value[1..];
    }
}
