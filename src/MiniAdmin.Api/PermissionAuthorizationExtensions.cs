using System.Security.Claims;
using MiniAdmin.Application.Contracts.Menus;

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

        var menuRepository = httpContext.RequestServices.GetRequiredService<IMenuRepository>();
        var currentPermissionCodes = await menuRepository.GetPermissionCodesByUserNameAsync(
            userName,
            cancellationToken);
        var currentPermissionSet = currentPermissionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return permissionCodes.Any(currentPermissionSet.Contains);
    }
}
