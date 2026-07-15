using System.Security.Claims;
using MiniAdmin.Platform.AspNetCore.Authorization;
using MiniAdmin.Platform.Authorization;

internal static class PermissionAuthorizationExtensions
{
    public static RouteHandlerBuilder RequirePermission(
        this RouteHandlerBuilder builder,
        string permissionCode)
    {
        return builder
            .RequireAuthorization()
            .AddEndpointFilter(async (context, next) =>
            {
                return await HasAnyCurrentPermissionAsync(
                    context.HttpContext,
                    [permissionCode],
                    context.HttpContext.RequestAborted)
                    ? await next(context)
                    : Results.Forbid();
            });
    }

    public static RouteHandlerBuilder RequireAnyPermission(
        this RouteHandlerBuilder builder,
        params string[] permissionCodes)
    {
        return builder
            .RequireAuthorization()
            .AddEndpointFilter(async (context, next) =>
            {
                return await HasAnyCurrentPermissionAsync(
                    context.HttpContext,
                    permissionCodes,
                    context.HttpContext.RequestAborted)
                    ? await next(context)
                    : Results.Forbid();
            });
    }

    public static bool HasPermission(this ClaimsPrincipal principal, string permissionCode)
    {
        return principal.HasClaim("permission", permissionCode);
    }

    private static async Task<bool> HasAnyCurrentPermissionAsync(
        HttpContext httpContext,
        IReadOnlyCollection<string> permissionCodes,
        CancellationToken cancellationToken)
    {
        var userName = httpContext.User.Identity?.Name
            ?? httpContext.User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(userName))
        {
            return false;
        }

        var decisionService = httpContext.RequestServices
            .GetRequiredService<IAuthorizationDecisionService>();
        AuthorizationDecision? lastDecision = null;
        foreach (var permissionCode in permissionCodes)
        {
            var (resource, action) = ParsePermission(permissionCode);
            lastDecision = await decisionService.AuthorizeAsync(
                new AuthorizationRequest(
                    httpContext.User,
                    permissionCode,
                    resource,
                    action,
                    AuthorizationRequestAttributeFactory.Create(httpContext)),
                cancellationToken);
            if (lastDecision.Allowed)
            {
                httpContext.Items[typeof(AuthorizationDecision)] = lastDecision;
                return true;
            }
        }

        if (lastDecision is not null)
        {
            httpContext.Items[typeof(AuthorizationDecision)] = lastDecision;
        }

        return false;
    }

    private static (string? Resource, string? Action) ParsePermission(string permissionCode)
    {
        var separatorIndex = permissionCode.LastIndexOf(':');
        return separatorIndex <= 0 || separatorIndex == permissionCode.Length - 1
            ? (null, null)
            : (
                permissionCode[..separatorIndex].Replace(':', '.'),
                permissionCode[(separatorIndex + 1)..]);
    }
}
