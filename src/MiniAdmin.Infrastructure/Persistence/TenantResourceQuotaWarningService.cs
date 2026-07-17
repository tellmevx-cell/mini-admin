using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.TenantResourceQuotas;
using MiniAdmin.Application.Contracts.UserNotifications;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Domain.Shared.MultiTenancy;
using MiniAdmin.Infrastructure.MultiTenancy;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class TenantResourceQuotaWarningService(
    MiniAdminDbContext dbContext,
    CurrentTenant currentTenant,
    IUserNotificationRepository userNotificationRepository,
    INotificationTemplateRenderer notificationTemplateRenderer)
    : ITenantResourceQuotaWarningService
{
    private const long BytesPerMb = 1024L * 1024L;
    private const string TemplateCode = "TenantQuota.Warning";
    private const string NotificationCategory = "TenantQuota";
    private const string NotificationSourceType = "TenantQuota";
    private static readonly SemaphoreSlim ScanGate = new(1, 1);

    public async Task<TenantResourceUsageDto?> GetCurrentUsageAsync(
        CancellationToken cancellationToken = default)
    {
        if (currentTenant.TenantId is not Guid tenantId)
        {
            return null;
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .Include(item => item.Package)
            .SingleOrDefaultAsync(item => item.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        var usedUsers = await dbContext.Users
            .AsNoTracking()
            .LongCountAsync(item => item.TenantId == tenantId, cancellationToken);
        var usedStorageBytes = await dbContext.ManagedFiles
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .SumAsync(item => (long?)item.Size, cancellationToken) ?? 0;
        var warningStates = await dbContext.TenantResourceQuotaWarnings
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .ToDictionaryAsync(item => item.ResourceType, StringComparer.OrdinalIgnoreCase, cancellationToken);

        return BuildUsage(tenant, usedUsers, usedStorageBytes, warningStates, DateTimeOffset.UtcNow);
    }

    public async Task<TenantResourceQuotaScanResult> ScanAsync(
        CancellationToken cancellationToken = default)
    {
        await ScanGate.WaitAsync(cancellationToken);
        var originalTenantId = currentTenant.TenantId;
        var originalTenantCode = currentTenant.TenantCode;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var tenants = await dbContext.Tenants
                .Include(item => item.Package)
                .Where(item =>
                    item.Status == TenantStatus.Active &&
                    (!item.ExpireAt.HasValue || item.ExpireAt.Value > now))
                .OrderBy(item => item.Code)
                .ToArrayAsync(cancellationToken);
            var details = new List<TenantResourceQuotaWarningDetail>();
            var totalNotificationCount = 0;

            foreach (var tenant in tenants)
            {
                var usedUsers = await dbContext.Users
                    .LongCountAsync(item => item.TenantId == tenant.Id, cancellationToken);
                var usedStorageBytes = await dbContext.ManagedFiles
                    .Where(item => item.TenantId == tenant.Id)
                    .SumAsync(item => (long?)item.Size, cancellationToken) ?? 0;
                var states = await dbContext.TenantResourceQuotaWarnings
                    .Where(item => item.TenantId == tenant.Id)
                    .ToDictionaryAsync(item => item.ResourceType, StringComparer.OrdinalIgnoreCase, cancellationToken);
                var resources = CreateResources(tenant, usedUsers, usedStorageBytes);
                var hasRisk = resources.Any(item => IsRisk(item.Status));
                var recipientIds = hasRisk
                    ? await ResolveTenantAdminUserIdsAsync(tenant.Id, cancellationToken)
                    : [];

                foreach (var resource in resources)
                {
                    if (!states.TryGetValue(resource.ResourceType, out var state))
                    {
                        state = new TenantResourceQuotaWarning
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            ResourceType = resource.ResourceType
                        };
                        dbContext.TenantResourceQuotaWarnings.Add(state);
                        states[resource.ResourceType] = state;
                    }

                    state.Status = resource.Status;
                    state.UsedValue = resource.UsedValue;
                    state.LimitValue = resource.LimitValue;
                    state.LastCheckedAt = now;

                    var notificationCount = 0;
                    if (IsRisk(resource.Status) &&
                        !string.Equals(
                            state.LastNotifiedStatus,
                            resource.Status,
                            StringComparison.OrdinalIgnoreCase) &&
                        recipientIds.Length > 0)
                    {
                        currentTenant.Change(tenant.Id, tenant.Code);
                        var nextSequence = checked(state.NotificationSequence + 1);
                        var rendered = await RenderNotificationAsync(
                            tenant,
                            resource,
                            cancellationToken);
                        notificationCount = await userNotificationRepository.CreateForUsersAsync(
                            recipientIds,
                            [new CreateUserNotificationRequest(
                                rendered.Title,
                                rendered.Message,
                                NotificationCategory,
                                resource.Status == TenantQuotaStatuses.Exhausted ? "Critical" : "Warning",
                                rendered.Link,
                                NotificationSourceType,
                                $"{tenant.Id:N}:{ResourceCode(resource.ResourceType)}:{nextSequence}")],
                            now,
                            cancellationToken);
                        state.NotificationSequence = nextSequence;
                        state.LastNotifiedStatus = resource.Status;
                        state.LastNotifiedAt = now;
                        totalNotificationCount += notificationCount;
                    }
                    else if (!IsRisk(resource.Status))
                    {
                        state.LastNotifiedStatus = null;
                    }

                    if (IsRisk(resource.Status))
                    {
                        details.Add(new TenantResourceQuotaWarningDetail(
                            tenant.Id.ToString(),
                            tenant.Code,
                            tenant.Name,
                            resource.ResourceType,
                            resource.DisplayName,
                            resource.UsedValue,
                            resource.LimitValue,
                            resource.UsagePercent,
                            resource.Status,
                            recipientIds.Length,
                            notificationCount,
                            state.LastNotifiedAt));
                    }
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return new TenantResourceQuotaScanResult(
                tenants.Length,
                details.Count(item => item.Status == TenantQuotaStatuses.Warning),
                details.Count(item => item.Status == TenantQuotaStatuses.Exhausted),
                totalNotificationCount,
                details);
        }
        finally
        {
            currentTenant.Change(originalTenantId, originalTenantCode);
            ScanGate.Release();
        }
    }

    private async Task<Guid[]> ResolveTenantAdminUserIdsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Where(user =>
                user.TenantId == tenantId &&
                user.IsEnabled &&
                user.UserRoles.Any(userRole =>
                    userRole.Role.Code == "tenant-admin" &&
                    userRole.Role.IsEnabled))
            .Select(user => user.Id)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    private async Task<NotificationTemplateRenderResult> RenderNotificationAsync(
        Tenant tenant,
        ResourceSnapshot resource,
        CancellationToken cancellationToken)
    {
        var statusText = resource.Status == TenantQuotaStatuses.Exhausted ? "已耗尽" : "即将耗尽";
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tenantCode"] = tenant.Code,
            ["tenantName"] = tenant.Name,
            ["resourceName"] = resource.DisplayName,
            ["used"] = FormatValue(resource.ResourceType, resource.UsedValue),
            ["limit"] = FormatValue(resource.ResourceType, resource.LimitValue),
            ["usagePercent"] = resource.UsagePercent.ToString("0.##"),
            ["statusText"] = statusText,
            ["managementPath"] = resource.ManagementPath
        };
        return await notificationTemplateRenderer.RenderAsync(
            TemplateCode,
            $"{resource.DisplayName}配额{statusText}",
            $"当前已使用 {variables["used"]} / {variables["limit"]}（{variables["usagePercent"]}%），请及时释放资源或联系平台管理员调整套餐。",
            resource.ManagementPath,
            variables,
            cancellationToken);
    }

    private static TenantResourceUsageDto BuildUsage(
        Tenant tenant,
        long usedUsers,
        long usedStorageBytes,
        IReadOnlyDictionary<string, TenantResourceQuotaWarning> warningStates,
        DateTimeOffset checkedAt)
    {
        var resources = CreateResources(tenant, usedUsers, usedStorageBytes);
        var users = ToMetric(resources[0], warningStates);
        var storage = ToMetric(resources[1], warningStates);
        return new TenantResourceUsageDto(
            tenant.Id.ToString(),
            tenant.Code,
            tenant.Name,
            tenant.Package?.Name,
            TenantQuotaStatuses.MostSevere(users.Status, storage.Status),
            users,
            storage,
            checkedAt);
    }

    private static ResourceSnapshot[] CreateResources(
        Tenant tenant,
        long usedUsers,
        long usedStorageBytes)
    {
        var maxUsers = Math.Max(tenant.Package?.MaxUsers ?? 0, 0);
        var maxStorageBytes = checked((long)Math.Max(tenant.Package?.MaxStorageMb ?? 0, 0) * BytesPerMb);
        return
        [
            CreateResource(
                TenantResourceTypes.Users,
                "用户账号",
                usedUsers,
                maxUsers,
                "/system/user"),
            CreateResource(
                TenantResourceTypes.Storage,
                "文件存储",
                usedStorageBytes,
                maxStorageBytes,
                "/system/file")
        ];
    }

    private static ResourceSnapshot CreateResource(
        string resourceType,
        string displayName,
        long usedValue,
        long limitValue,
        string managementPath)
    {
        return new ResourceSnapshot(
            resourceType,
            displayName,
            usedValue,
            limitValue,
            CalculatePercent(usedValue, limitValue),
            TenantQuotaStatuses.Evaluate(usedValue, limitValue),
            managementPath);
    }

    private static TenantResourceMetricDto ToMetric(
        ResourceSnapshot resource,
        IReadOnlyDictionary<string, TenantResourceQuotaWarning> warningStates)
    {
        warningStates.TryGetValue(resource.ResourceType, out var state);
        return new TenantResourceMetricDto(
            resource.ResourceType,
            resource.DisplayName,
            resource.UsedValue,
            resource.LimitValue,
            resource.UsagePercent,
            resource.Status,
            state?.LastNotifiedAt,
            resource.ManagementPath);
    }

    private static decimal CalculatePercent(long usedValue, long limitValue)
    {
        return limitValue <= 0
            ? 0
            : Math.Round(usedValue * 100m / limitValue, 2, MidpointRounding.AwayFromZero);
    }

    private static bool IsRisk(string status)
    {
        return status is TenantQuotaStatuses.Warning or TenantQuotaStatuses.Exhausted;
    }

    private static string ResourceCode(string resourceType)
    {
        return resourceType == TenantResourceTypes.Users ? "usr" : "sto";
    }

    private static string FormatValue(string resourceType, long value)
    {
        if (resourceType == TenantResourceTypes.Users)
        {
            return $"{value} 个";
        }

        if (value >= 1024L * 1024L * 1024L)
        {
            return $"{value / (1024m * 1024m * 1024m):0.##} GB";
        }

        return $"{value / (1024m * 1024m):0.##} MB";
    }

    private sealed record ResourceSnapshot(
        string ResourceType,
        string DisplayName,
        long UsedValue,
        long LimitValue,
        decimal UsagePercent,
        string Status,
        string ManagementPath);
}
