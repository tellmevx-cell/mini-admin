namespace MiniAdmin.Application.Contracts.TenantResourceQuotas;

public sealed record TenantResourceQuotaSnapshot(
    Guid TenantId,
    int UsedUsers,
    int MaxUsers,
    long UsedStorageBytes,
    long MaxStorageBytes)
{
    public bool IsUserUnlimited => MaxUsers == 0;

    public bool IsStorageUnlimited => MaxStorageBytes == 0;

    public bool IsUserExceeded => !IsUserUnlimited && UsedUsers > MaxUsers;

    public bool IsStorageExceeded => !IsStorageUnlimited && UsedStorageBytes > MaxStorageBytes;
}

public static class TenantResourceTypes
{
    public const string Users = "Users";

    public const string Storage = "Storage";
}

public static class TenantQuotaStatuses
{
    public const string Unlimited = "Unlimited";

    public const string Normal = "Normal";

    public const string Warning = "Warning";

    public const string Exhausted = "Exhausted";

    public static string Evaluate(long used, long limit)
    {
        if (limit <= 0)
        {
            return Unlimited;
        }

        if (used >= limit)
        {
            return Exhausted;
        }

        return used / (decimal)limit >= 0.8m ? Warning : Normal;
    }

    public static string MostSevere(params string[] statuses)
    {
        return statuses.OrderByDescending(GetSeverity).FirstOrDefault() ?? Normal;
    }

    private static int GetSeverity(string status)
    {
        return status switch
        {
            Exhausted => 3,
            Warning => 2,
            Normal => 1,
            _ => 0
        };
    }
}

public sealed record TenantResourceMetricDto(
    string ResourceType,
    string DisplayName,
    long UsedValue,
    long LimitValue,
    decimal UsagePercent,
    string Status,
    DateTimeOffset? LastNotifiedAt,
    string ManagementPath);

public sealed record TenantResourceUsageDto(
    string TenantId,
    string TenantCode,
    string TenantName,
    string? PackageName,
    string OverallStatus,
    TenantResourceMetricDto Users,
    TenantResourceMetricDto Storage,
    DateTimeOffset CheckedAt);

public sealed record TenantResourceQuotaWarningDetail(
    string TenantId,
    string TenantCode,
    string TenantName,
    string ResourceType,
    string ResourceName,
    long UsedValue,
    long LimitValue,
    decimal UsagePercent,
    string Status,
    int RecipientCount,
    int NotificationCount,
    DateTimeOffset? LastNotifiedAt);

public sealed record TenantResourceQuotaScanResult(
    int ScannedTenantCount,
    int WarningResourceCount,
    int ExhaustedResourceCount,
    int NotificationCount,
    IReadOnlyList<TenantResourceQuotaWarningDetail> Details);

public sealed class TenantResourceQuotaExceededException(
    string resource,
    long used,
    long limit,
    long requested,
    string message) : Exception(message)
{
    public string Resource { get; } = resource;

    public long Used { get; } = used;

    public long Limit { get; } = limit;

    public long Requested { get; } = requested;
}

public interface ITenantResourceQuotaService
{
    Task<TenantResourceQuotaSnapshot?> GetCurrentAsync(
        CancellationToken cancellationToken = default);

    Task EnsureCanAddUsersAsync(
        int additionalUsers,
        CancellationToken cancellationToken = default);

    Task EnsureCanAddStorageAsync(
        long additionalBytes,
        CancellationToken cancellationToken = default);

    Task<TResult> ExecuteUserWriteAsync<TResult>(
        int additionalUsers,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default);

    Task<TResult> ExecuteStorageWriteAsync<TResult>(
        long additionalBytes,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default);
}

public interface ITenantResourceQuotaWarningService
{
    Task<TenantResourceUsageDto?> GetCurrentUsageAsync(
        CancellationToken cancellationToken = default);

    Task<TenantResourceQuotaScanResult> ScanAsync(
        CancellationToken cancellationToken = default);
}
