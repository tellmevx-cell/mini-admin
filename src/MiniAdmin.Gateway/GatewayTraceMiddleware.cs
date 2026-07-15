using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MiniAdmin.Gateway;

public sealed partial class GatewayTraceMiddleware(
    RequestDelegate next,
    ILogger<GatewayTraceMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var suppliedTraceId = context.Request.Headers["X-Trace-Id"].ToString();
        var traceId = IsValidTraceId(suppliedTraceId)
            ? suppliedTraceId
            : Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        context.Request.Headers["X-Trace-Id"] = traceId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Trace-Id"] = traceId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object?>
               {
                   ["TraceId"] = traceId,
                   ["RequestId"] = context.TraceIdentifier
               }))
        {
            await next(context);
        }
    }

    private static bool IsValidTraceId(string value)
    {
        return value.Length is >= 16 and <= 64 && TraceIdPattern().IsMatch(value);
    }

    [GeneratedRegex("^[a-zA-Z0-9_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex TraceIdPattern();
}
