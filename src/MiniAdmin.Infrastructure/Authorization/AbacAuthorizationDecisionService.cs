using System.Security.Claims;
using MiniAdmin.Application.Contracts.Menus;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.OpenPlatform;
using MiniAdmin.Platform.Authorization;

namespace MiniAdmin.Infrastructure.Authorization;

public sealed class AbacAuthorizationDecisionService(
    IMenuRepository menuRepository,
    IAbacPolicyProvider policyProvider,
    ICurrentTenant currentTenant) : IAuthorizationDecisionService
{
    public async Task<AuthorizationDecision> AuthorizeAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Principal.Identity?.IsAuthenticated != true)
        {
            return AuthorizationDecision.Deny("The request is not authenticated.");
        }

        var principalType = request.Principal.FindFirst(OpenPlatformClaimTypes.PrincipalType)?.Value;
        var isExternalCredential = string.Equals(
                principalType,
                OpenPlatformClaimTypes.Application,
                StringComparison.Ordinal) ||
            string.Equals(principalType, OpenPlatformClaimTypes.AppKey, StringComparison.Ordinal);
        var userName = string.Equals(principalType, OpenPlatformClaimTypes.Application, StringComparison.Ordinal)
            ? request.Principal.FindFirst(OpenPlatformClaimTypes.ClientId)?.Value
            : request.Principal.Identity.Name ?? request.Principal.FindFirst(ClaimTypes.Name)?.Value;
        if (string.IsNullOrWhiteSpace(userName))
        {
            return AuthorizationDecision.Deny("The authenticated identity has no user name.");
        }

        if (!string.IsNullOrWhiteSpace(request.Permission))
        {
            var grantedPermissions = isExternalCredential
                ? request.Principal.FindAll("permission")
                    .Select(claim => claim.Value)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : await menuRepository.GetPermissionCodesByUserNameAsync(
                    userName,
                    cancellationToken);
            if (!grantedPermissions.Any(permission =>
                    PatternMatches(permission, request.Permission, ':')))
            {
                return AuthorizationDecision.Deny($"Missing permission '{request.Permission}'.");
            }
        }

        var (resource, action) = ResolveResourceAndAction(request);
        if (string.IsNullOrWhiteSpace(resource) || string.IsNullOrWhiteSpace(action))
        {
            return AuthorizationDecision.Allow("RBAC permission matched; no ABAC resource was declared.");
        }

        var policies = await policyProvider.GetPoliciesAsync(
            currentTenant.TenantId,
            cancellationToken);
        var applicablePolicies = policies
            .Where(policy => policy.IsEnabled)
            .Where(policy => SubjectMatches(policy, request.Principal, userName))
            .Where(policy => PatternMatches(policy.Resource, resource, '.'))
            .Where(policy => PatternMatches(policy.Action, action, ':'))
            .OrderByDescending(policy => policy.Priority)
            .ToArray();

        if (applicablePolicies.Length == 0)
        {
            return AuthorizationDecision.Allow("RBAC permission matched; no ABAC policy applied.");
        }

        var matchedAllowPolicies = new List<Guid>();
        var hasAllowPolicies = false;
        foreach (var policy in applicablePolicies)
        {
            var isAllow = policy.Effect.Equals("Allow", StringComparison.OrdinalIgnoreCase);
            hasAllowPolicies |= isAllow;

            bool matched;
            try
            {
                matched = AbacConditionEvaluator.Evaluate(
                    policy.ConditionsJson,
                    request.Attributes);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or System.Text.Json.JsonException)
            {
                return AuthorizationDecision.Deny(
                    $"ABAC policy '{policy.Id}' is invalid and was rejected safely.",
                    policy.Id);
            }

            if (!matched)
            {
                continue;
            }

            if (policy.Effect.Equals("Deny", StringComparison.OrdinalIgnoreCase))
            {
                return AuthorizationDecision.Deny(
                    $"Explicit ABAC deny policy '{policy.Id}' matched.",
                    policy.Id);
            }

            if (isAllow)
            {
                matchedAllowPolicies.Add(policy.Id);
            }
        }

        if (hasAllowPolicies && matchedAllowPolicies.Count == 0)
        {
            return AuthorizationDecision.Deny(
                "ABAC allow policies apply, but none of their conditions matched.");
        }

        return matchedAllowPolicies.Count > 0
            ? AuthorizationDecision.Allow(
                "RBAC and ABAC allow policies matched.",
                matchedAllowPolicies.ToArray())
            : AuthorizationDecision.Allow("RBAC permission matched; no ABAC deny policy matched.");
    }

    private static (string? Resource, string? Action) ResolveResourceAndAction(
        AuthorizationRequest request)
    {
        var resource = request.Resource;
        var action = request.Action;
        if ((!string.IsNullOrWhiteSpace(resource) && !string.IsNullOrWhiteSpace(action)) ||
            string.IsNullOrWhiteSpace(request.Permission))
        {
            return (resource, action);
        }

        var separatorIndex = request.Permission.LastIndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == request.Permission.Length - 1)
        {
            return (resource, action);
        }

        return (
            resource ?? request.Permission[..separatorIndex].Replace(':', '.'),
            action ?? request.Permission[(separatorIndex + 1)..]);
    }

    private static bool SubjectMatches(
        AbacPolicySnapshot policy,
        ClaimsPrincipal principal,
        string userName)
    {
        if (policy.SubjectType.Equals("Any", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(policy.SubjectId))
        {
            return false;
        }

        if (policy.SubjectType.Equals("User", StringComparison.OrdinalIgnoreCase))
        {
            return policy.SubjectId.Equals(userName, StringComparison.OrdinalIgnoreCase) ||
                principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?.Equals(policy.SubjectId, StringComparison.OrdinalIgnoreCase) == true;
        }

        if (policy.SubjectType.Equals("Application", StringComparison.OrdinalIgnoreCase))
        {
            return principal.FindFirst(OpenPlatformClaimTypes.ClientId)?.Value
                ?.Equals(policy.SubjectId, StringComparison.OrdinalIgnoreCase) == true;
        }

        return policy.SubjectType.Equals("Role", StringComparison.OrdinalIgnoreCase) &&
            principal.FindAll(ClaimTypes.Role)
                .Any(claim => claim.Value.Equals(policy.SubjectId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool PatternMatches(string pattern, string value, char separator)
    {
        if (pattern.Equals("*", StringComparison.OrdinalIgnoreCase) ||
            pattern.Equals(value, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var patternParts = pattern.Split(separator, StringSplitOptions.TrimEntries);
        var valueParts = value.Split(separator, StringSplitOptions.TrimEntries);
        return patternParts.Length == valueParts.Length &&
            patternParts.Zip(valueParts).All(parts =>
                parts.First == "*" ||
                parts.First.Equals(parts.Second, StringComparison.OrdinalIgnoreCase));
    }
}
