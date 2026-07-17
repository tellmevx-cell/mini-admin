using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Tenants;
using MiniAdmin.Application.Contracts.UserNotifications;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Domain.Shared.MultiTenancy;
using MiniAdmin.Infrastructure.MultiTenancy;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class TenantLifecycleService(
    MiniAdminDbContext dbContext,
    TenantSessionInvalidator tenantSessionInvalidator,
    CurrentTenant currentTenant,
    IUserNotificationRepository userNotificationRepository,
    INotificationTemplateRenderer notificationTemplateRenderer) : ITenantLifecycleService
{
    private const string ReminderTemplateCode = "TenantLifecycle.ExpiryReminder";
    private const string ExpiredTemplateCode = "TenantLifecycle.Expired";
    private const string NotificationCategory = "TenantLifecycle";
    private const string NotificationSourceType = "TenantLifecycle";
    private static readonly SemaphoreSlim ScanGate = new(1, 1);

    public async Task<PageResult<TenantLifecycleRecordDto>> GetRecordsAsync(
        Guid tenantId,
        TenantLifecycleRecordListQuery query,
        CancellationToken cancellationToken = default)
    {
        var recordsQuery = dbContext.TenantLifecycleRecords
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            var eventType = query.EventType.Trim();
            recordsQuery = recordsQuery.Where(item => item.EventType == eventType);
        }

        var total = await recordsQuery.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var items = await recordsQuery
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new TenantLifecycleRecordDto(
                item.Id.ToString(),
                item.TenantId.ToString(),
                item.EventType,
                item.Source,
                item.OperatorUserId.HasValue ? item.OperatorUserId.Value.ToString() : null,
                item.OperatorUserName,
                item.FromStatus,
                item.ToStatus,
                item.PreviousExpireAt,
                item.NewExpireAt,
                item.PreviousPackageId.HasValue ? item.PreviousPackageId.Value.ToString() : null,
                item.NewPackageId.HasValue ? item.NewPackageId.Value.ToString() : null,
                item.ReminderDays,
                item.Description,
                item.CreatedAt))
            .ToArrayAsync(cancellationToken);

        return new PageResult<TenantLifecycleRecordDto>(items, total);
    }

    public async Task<TenantLifecycleScanResult> ScanAsync(
        CancellationToken cancellationToken = default)
    {
        await ScanGate.WaitAsync(cancellationToken);
        var originalTenantId = currentTenant.TenantId;
        var originalTenantCode = currentTenant.TenantCode;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var warningBoundary = now.AddDays(30);
            var tenants = await dbContext.Tenants
                .Where(item =>
                    item.Status == TenantStatus.Active &&
                    item.ExpireAt.HasValue &&
                    item.ExpireAt.Value <= warningBoundary)
                .OrderBy(item => item.ExpireAt)
                .ToArrayAsync(cancellationToken);
            var details = new List<TenantLifecycleScanDetail>();
            var reminderCount = 0;
            var expiredCount = 0;
            var notificationCount = 0;

            foreach (var tenant in tenants)
            {
                var expireAt = tenant.ExpireAt!.Value;
                if (expireAt <= now)
                {
                    var result = await ExpireTenantAsync(tenant, expireAt, now, cancellationToken);
                    expiredCount++;
                    notificationCount += result.NotificationCount;
                    details.Add(result.Detail);
                    continue;
                }

                var reminderDays = ResolveReminderDays(expireAt - now);
                var deduplicationKey = BuildDeduplicationKey(tenant.Id, expireAt, reminderDays);
                var exists = await dbContext.TenantLifecycleRecords
                    .AsNoTracking()
                    .AnyAsync(item => item.DeduplicationKey == deduplicationKey, cancellationToken);
                if (exists)
                {
                    continue;
                }

                var recipientIds = await ResolveTenantAdminUserIdsAsync(tenant.Id, cancellationToken);
                if (recipientIds.Length == 0)
                {
                    details.Add(new TenantLifecycleScanDetail(
                        tenant.Id.ToString(),
                        tenant.Code,
                        tenant.Name,
                        TenantLifecycleEventTypes.ExpiryReminder,
                        expireAt,
                        reminderDays,
                        0,
                        0,
                        $"租户将在 {reminderDays} 天内到期，但未找到启用的租户管理员"));
                    continue;
                }

                currentTenant.Change(tenant.Id, tenant.Code);
                var rendered = await RenderReminderAsync(tenant, expireAt, reminderDays, cancellationToken);
                var createdCount = await userNotificationRepository.CreateForUsersAsync(
                    recipientIds,
                    [new CreateUserNotificationRequest(
                        rendered.Title,
                        rendered.Message,
                        NotificationCategory,
                        reminderDays <= 1 ? "Critical" : "Warning",
                        rendered.Link,
                        NotificationSourceType,
                        BuildNotificationSourceId(tenant.Id, expireAt, $"r{reminderDays}"))],
                    now,
                    cancellationToken);

                dbContext.TenantLifecycleRecords.Add(new TenantLifecycleRecord
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    EventType = TenantLifecycleEventTypes.ExpiryReminder,
                    Source = TenantLifecycleSources.Scheduled,
                    FromStatus = TenantStatus.Active.ToString(),
                    ToStatus = TenantStatus.Active.ToString(),
                    PreviousExpireAt = expireAt,
                    NewExpireAt = expireAt,
                    ReminderDays = reminderDays,
                    Description = $"已发送 {reminderDays} 天到期提醒",
                    DeduplicationKey = deduplicationKey,
                    CreatedAt = now
                });
                reminderCount++;
                notificationCount += createdCount;
                details.Add(new TenantLifecycleScanDetail(
                    tenant.Id.ToString(),
                    tenant.Code,
                    tenant.Name,
                    TenantLifecycleEventTypes.ExpiryReminder,
                    expireAt,
                    reminderDays,
                    recipientIds.Length,
                    createdCount,
                    $"已进入 {reminderDays} 天到期提醒区间"));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return new TenantLifecycleScanResult(
                tenants.Length,
                reminderCount,
                expiredCount,
                notificationCount,
                details);
        }
        finally
        {
            currentTenant.Change(originalTenantId, originalTenantCode);
            ScanGate.Release();
        }
    }

    private async Task<(TenantLifecycleScanDetail Detail, int NotificationCount)> ExpireTenantAsync(
        Tenant tenant,
        DateTimeOffset expireAt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        tenant.Status = TenantStatus.Expired;
        tenant.UpdatedAt = now;
        await tenantSessionInvalidator.InvalidateAsync(tenant.Id, cancellationToken);

        var deduplicationKey = BuildDeduplicationKey(tenant.Id, expireAt, 0);
        dbContext.TenantLifecycleRecords.Add(new TenantLifecycleRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            EventType = TenantLifecycleEventTypes.Expired,
            Source = TenantLifecycleSources.Scheduled,
            FromStatus = TenantStatus.Active.ToString(),
            ToStatus = TenantStatus.Expired.ToString(),
            PreviousExpireAt = expireAt,
            NewExpireAt = expireAt,
            Description = "租户已到期，状态已自动更新且用户会话已失效",
            DeduplicationKey = deduplicationKey,
            CreatedAt = now
        });

        currentTenant.Change(null, null);
        var recipientIds = await ResolvePlatformAdminUserIdsAsync(cancellationToken);
        var createdCount = 0;
        if (recipientIds.Length > 0)
        {
            var rendered = await RenderExpiredAsync(tenant, expireAt, cancellationToken);
            createdCount = await userNotificationRepository.CreateForUsersAsync(
                recipientIds,
                [new CreateUserNotificationRequest(
                    rendered.Title,
                    rendered.Message,
                    NotificationCategory,
                    "Warning",
                    rendered.Link,
                    NotificationSourceType,
                    BuildNotificationSourceId(tenant.Id, expireAt, "expired"))],
                now,
                cancellationToken);
        }

        return (new TenantLifecycleScanDetail(
            tenant.Id.ToString(),
            tenant.Code,
            tenant.Name,
            TenantLifecycleEventTypes.Expired,
            expireAt,
            null,
            recipientIds.Length,
            createdCount,
            "租户已自动过期，用户会话已失效"), createdCount);
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

    private async Task<Guid[]> ResolvePlatformAdminUserIdsAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Where(user =>
                !user.TenantId.HasValue &&
                user.IsEnabled &&
                user.UserRoles.Any(userRole =>
                    userRole.Role.Code == "admin" &&
                    userRole.Role.IsEnabled))
            .Select(user => user.Id)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    private Task<NotificationTemplateRenderResult> RenderReminderAsync(
        Tenant tenant,
        DateTimeOffset expireAt,
        int reminderDays,
        CancellationToken cancellationToken)
    {
        var variables = CreateVariables(tenant, expireAt);
        variables["reminderDays"] = reminderDays.ToString();
        return notificationTemplateRenderer.RenderAsync(
            ReminderTemplateCode,
            $"租户将在 {reminderDays} 天内到期",
            $"{tenant.Name}（{tenant.Code}）将在 {expireAt:yyyy-MM-dd HH:mm} 到期，请及时联系平台管理员续期。",
            "/dashboard/workspace",
            variables,
            cancellationToken);
    }

    private Task<NotificationTemplateRenderResult> RenderExpiredAsync(
        Tenant tenant,
        DateTimeOffset expireAt,
        CancellationToken cancellationToken)
    {
        return notificationTemplateRenderer.RenderAsync(
            ExpiredTemplateCode,
            $"租户已到期：{tenant.Name}",
            $"{tenant.Name}（{tenant.Code}）已于 {expireAt:yyyy-MM-dd HH:mm} 到期，系统已停止租户访问。",
            $"/platform/tenant?code={Uri.EscapeDataString(tenant.Code)}&status=Expired",
            CreateVariables(tenant, expireAt),
            cancellationToken);
    }

    private static Dictionary<string, string> CreateVariables(Tenant tenant, DateTimeOffset expireAt)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tenantId"] = tenant.Id.ToString(),
            ["tenantCode"] = tenant.Code,
            ["tenantName"] = tenant.Name,
            ["expireAt"] = expireAt.ToString("yyyy-MM-dd HH:mm:ss"),
            ["managementPath"] = "/platform/tenant"
        };
    }

    private static int ResolveReminderDays(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.FromDays(1))
        {
            return 1;
        }

        return remaining <= TimeSpan.FromDays(7) ? 7 : 30;
    }

    private static string BuildDeduplicationKey(
        Guid tenantId,
        DateTimeOffset expireAt,
        int reminderDays)
    {
        var suffix = reminderDays == 0 ? "expired" : $"reminder:{reminderDays}";
        return $"{tenantId:N}:{expireAt.UtcDateTime.Ticks}:{suffix}";
    }

    private static string BuildNotificationSourceId(
        Guid tenantId,
        DateTimeOffset expireAt,
        string suffix)
    {
        return $"{tenantId:N}:{expireAt.UtcDateTime.Ticks}:{suffix}";
    }
}
