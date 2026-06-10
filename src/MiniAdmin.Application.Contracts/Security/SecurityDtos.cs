using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.Security;

public sealed record SecurityEventDto(
    string Id,
    string EventType,
    string Level,
    string? UserId,
    string? UserName,
    string? IpAddress,
    string? UserAgent,
    string Title,
    string Description,
    string? RelatedEntityType,
    string? RelatedEntityId,
    DateTimeOffset CreatedAt);

public sealed record SecurityEventListQuery(
    int Page = 1,
    int PageSize = 20,
    string? EventType = null,
    string? Level = null,
    string? UserName = null,
    string? CurrentUserName = null);

public sealed record SaveSecurityEventRequest(
    string EventType,
    string Level,
    string Title,
    string Description,
    Guid? UserId = null,
    string? UserName = null,
    string? IpAddress = null,
    string? UserAgent = null,
    string? RelatedEntityType = null,
    string? RelatedEntityId = null);

public sealed record SecurityCenterOverviewDto(
    SecurityAccountSummaryDto Account,
    SecurityLoginSummaryDto Login,
    SecurityPermissionSummaryDto Permission,
    SecuritySessionSummaryDto Session,
    IReadOnlyList<SecurityEventDto> RecentEvents);

public sealed record SecurityAccountSummaryDto(
    int TotalUserCount,
    int EnabledUserCount,
    int DisabledUserCount,
    int LockedUserCount,
    int StaleUserCount);

public sealed record SecurityLoginSummaryDto(
    int FailedLoginCount24h,
    int FailedUserCount24h,
    int FailedIpCount24h);

public sealed record SecurityPermissionSummaryDto(
    int PermissionChangeCount24h,
    IReadOnlyList<SecurityEventDto> RecentHighRiskEvents);

public sealed record SecuritySessionSummaryDto(
    int OnlineUserCount,
    IReadOnlyList<SecurityEventDto> RecentForceLogoutEvents);

public sealed record SecurityPolicyDto(
    int CaptchaRequiredFailures,
    int LockoutFailures,
    int LockoutMinutes,
    int CaptchaExpireSeconds,
    int OnlineActiveTimeoutMinutes,
    int OnlineTouchThrottleSeconds,
    int StaleUserDays);

public sealed record UpdateSecurityPolicyRequest(
    int CaptchaRequiredFailures,
    int LockoutFailures,
    int LockoutMinutes,
    int CaptchaExpireSeconds,
    int OnlineActiveTimeoutMinutes,
    int OnlineTouchThrottleSeconds,
    int StaleUserDays);

public interface ISecurityCenterAppService
{
    Task<SecurityCenterOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<PageResult<SecurityEventDto>> GetEventsAsync(
        SecurityEventListQuery query,
        CancellationToken cancellationToken = default);

    Task RecordEventAsync(
        SaveSecurityEventRequest request,
        CancellationToken cancellationToken = default);
}

public interface ISecurityPolicyAppService
{
    Task<SecurityPolicyDto> GetPolicyAsync(CancellationToken cancellationToken = default);

    Task<SecurityPolicyDto> UpdatePolicyAsync(
        UpdateSecurityPolicyRequest request,
        CancellationToken cancellationToken = default);
}

public interface ISecurityPolicyRepository
{
    Task<SecurityPolicyDto> GetPolicyAsync(CancellationToken cancellationToken = default);

    Task<SecurityPolicyDto> UpdatePolicyAsync(
        UpdateSecurityPolicyRequest request,
        CancellationToken cancellationToken = default);
}

public interface ISecurityEventRepository
{
    Task<SecurityCenterOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<PageResult<SecurityEventDto>> GetEventsAsync(
        SecurityEventListQuery query,
        CancellationToken cancellationToken = default);

    Task RecordEventAsync(
        SaveSecurityEventRequest request,
        CancellationToken cancellationToken = default);
}
