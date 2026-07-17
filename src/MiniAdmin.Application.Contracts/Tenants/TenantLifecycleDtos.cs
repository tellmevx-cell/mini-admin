using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.Tenants;

public static class TenantLifecycleEventTypes
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string PackageChanged = "PackageChanged";
    public const string ExpirationChanged = "ExpirationChanged";
    public const string Renewed = "Renewed";
    public const string Enabled = "Enabled";
    public const string Disabled = "Disabled";
    public const string ExpiryReminder = "ExpiryReminder";
    public const string Expired = "Expired";
}

public static class TenantLifecycleSources
{
    public const string Manual = "Manual";
    public const string Scheduled = "Scheduled";
    public const string System = "System";
}

public sealed record TenantOperationActor(
    Guid? UserId,
    string? UserName,
    string Source = TenantLifecycleSources.Manual);

public sealed record RenewTenantRequest(
    DateTimeOffset ExpireAt,
    bool Reactivate = true,
    string? Remark = null);

public sealed record TenantLifecycleRecordListQuery(
    int Page = 1,
    int PageSize = 20,
    string? EventType = null);

public sealed record TenantLifecycleRecordDto(
    string Id,
    string TenantId,
    string EventType,
    string Source,
    string? OperatorUserId,
    string? OperatorUserName,
    string? FromStatus,
    string? ToStatus,
    DateTimeOffset? PreviousExpireAt,
    DateTimeOffset? NewExpireAt,
    string? PreviousPackageId,
    string? NewPackageId,
    int? ReminderDays,
    string Description,
    DateTimeOffset CreatedAt);

public sealed record TenantLifecycleScanDetail(
    string TenantId,
    string TenantCode,
    string TenantName,
    string EventType,
    DateTimeOffset ExpireAt,
    int? ReminderDays,
    int RecipientCount,
    int NotificationCount,
    string Description);

public sealed record TenantLifecycleScanResult(
    int ScannedTenantCount,
    int ReminderCount,
    int ExpiredCount,
    int NotificationCount,
    IReadOnlyList<TenantLifecycleScanDetail> Details);

public interface ITenantLifecycleService
{
    Task<PageResult<TenantLifecycleRecordDto>> GetRecordsAsync(
        Guid tenantId,
        TenantLifecycleRecordListQuery query,
        CancellationToken cancellationToken = default);

    Task<TenantLifecycleScanResult> ScanAsync(
        CancellationToken cancellationToken = default);
}
