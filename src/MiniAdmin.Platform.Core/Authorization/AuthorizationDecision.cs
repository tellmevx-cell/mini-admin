using System.Security.Claims;

namespace MiniAdmin.Platform.Authorization;

public sealed record AuthorizationRequest(
    ClaimsPrincipal Principal,
    string? Permission,
    string? Resource,
    string? Action,
    IReadOnlyDictionary<string, object?> Attributes);

public sealed record AuthorizationDecision(
    bool Allowed,
    string Reason,
    IReadOnlyList<Guid> MatchedPolicyIds)
{
    public static AuthorizationDecision Allow(string reason, params Guid[] policyIds)
    {
        return new AuthorizationDecision(true, reason, policyIds);
    }

    public static AuthorizationDecision Deny(string reason, params Guid[] policyIds)
    {
        return new AuthorizationDecision(false, reason, policyIds);
    }
}

public interface IAuthorizationDecisionService
{
    Task<AuthorizationDecision> AuthorizeAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record AbacPolicySnapshot(
    Guid Id,
    Guid? TenantId,
    string SubjectType,
    string? SubjectId,
    string Resource,
    string Action,
    string Effect,
    string ConditionsJson,
    int Priority,
    bool IsEnabled);

public interface IAbacPolicyProvider
{
    Task<IReadOnlyList<AbacPolicySnapshot>> GetPoliciesAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}
