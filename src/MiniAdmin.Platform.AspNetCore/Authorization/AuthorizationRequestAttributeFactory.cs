using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace MiniAdmin.Platform.AspNetCore.Authorization;

public static class AuthorizationRequestAttributeFactory
{
    public static IReadOnlyDictionary<string, object?> Create(HttpContext httpContext)
    {
        var request = httpContext.Request;
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["user.id"] = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier),
            ["user.name"] = httpContext.User.Identity?.Name,
            ["user.roles"] = httpContext.User.FindAll(ClaimTypes.Role)
                .Select(claim => claim.Value)
                .ToArray(),
            ["tenant.id"] = httpContext.User.FindFirstValue("tenant_id"),
            ["tenant.code"] = httpContext.User.FindFirstValue("tenant_code"),
            ["request.method"] = request.Method,
            ["request.path"] = request.Path.Value,
            ["request.ip"] = httpContext.Connection.RemoteIpAddress?.ToString(),
            ["request.time"] = DateTimeOffset.UtcNow.ToString("O")
        };

        foreach (var header in request.Headers)
        {
            attributes[$"request.header.{header.Key}"] = header.Value.ToString();
        }

        foreach (var query in request.Query)
        {
            attributes[$"request.query.{query.Key}"] = query.Value.ToString();
        }

        foreach (var routeValue in request.RouteValues)
        {
            attributes[$"request.route.{routeValue.Key}"] = routeValue.Value;
        }

        return attributes;
    }
}
