using MiniAdmin.Platform.Authorization;

namespace MiniAdmin.Platform.AspNetCore.Authorization;

internal sealed class ClaimsAuthorizationDecisionService : IAuthorizationDecisionService
{
    public Task<AuthorizationDecision> AuthorizeAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Principal.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult(AuthorizationDecision.Deny("The request is not authenticated."));
        }

        if (string.IsNullOrWhiteSpace(request.Permission))
        {
            return Task.FromResult(AuthorizationDecision.Allow("Authenticated access."));
        }

        var granted = request.Principal
            .FindAll("permission")
            .Select(claim => claim.Value)
            .Any(value => PermissionMatches(value, request.Permission));

        return Task.FromResult(granted
            ? AuthorizationDecision.Allow("RBAC permission matched the current identity.")
            : AuthorizationDecision.Deny($"Missing permission '{request.Permission}'."));
    }

    private static bool PermissionMatches(string granted, string required)
    {
        if (granted.Equals("*", StringComparison.OrdinalIgnoreCase) ||
            granted.Equals(required, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var grantedParts = granted.Split(':', StringSplitOptions.TrimEntries);
        var requiredParts = required.Split(':', StringSplitOptions.TrimEntries);
        if (grantedParts.Length != requiredParts.Length)
        {
            return false;
        }

        return grantedParts
            .Zip(requiredParts)
            .All(parts => parts.First == "*" ||
                parts.First.Equals(parts.Second, StringComparison.OrdinalIgnoreCase));
    }
}
