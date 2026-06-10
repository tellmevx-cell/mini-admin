using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.UserNotifications;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfNotificationPolicyRepository(MiniAdminDbContext dbContext) : INotificationPolicyRepository
{
    public async Task<PageResult<NotificationPolicyDto>> GetListAsync(
        NotificationPolicyListQuery query,
        CancellationToken cancellationToken = default)
    {
        var policiesQuery = dbContext.NotificationPolicies.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            policiesQuery = policiesQuery.Where(policy =>
                policy.EventCode.Contains(keyword) ||
                policy.EventName.Contains(keyword) ||
                (policy.Remark != null && policy.Remark.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim();
            policiesQuery = policiesQuery.Where(policy => policy.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(query.EventCode))
        {
            var eventCode = query.EventCode.Trim();
            policiesQuery = policiesQuery.Where(policy => policy.EventCode == eventCode);
        }

        if (query.IsEnabled.HasValue)
        {
            policiesQuery = policiesQuery.Where(policy => policy.IsEnabled == query.IsEnabled.Value);
        }

        var total = await policiesQuery.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var items = await policiesQuery
            .OrderBy(policy => policy.Category)
            .ThenBy(policy => policy.EventCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(policy => ToDto(policy))
            .ToArrayAsync(cancellationToken);

        return new PageResult<NotificationPolicyDto>(items, total);
    }

    public async Task<NotificationPolicyDto?> FindByEventCodeAsync(
        string eventCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedEventCode = eventCode.Trim();
        return await dbContext.NotificationPolicies
            .AsNoTracking()
            .Where(policy => policy.EventCode == normalizedEventCode)
            .Select(policy => ToDto(policy))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<NotificationPolicyDto?> UpdateAsync(
        Guid id,
        SaveNotificationPolicyRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var policy = await dbContext.NotificationPolicies.SingleOrDefaultAsync(
            item => item.Id == id,
            cancellationToken);
        if (policy is null)
        {
            return null;
        }

        policy.EventName = request.EventName.Trim();
        policy.Category = request.Category.Trim();
        policy.RecipientStrategy = request.RecipientStrategy.Trim();
        policy.EnableInApp = request.EnableInApp;
        policy.EnableEmail = request.EnableEmail;
        policy.EnableWebhook = request.EnableWebhook;
        policy.IsEnabled = request.IsEnabled;
        policy.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
        policy.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(policy);
    }

    private static NotificationPolicyDto ToDto(NotificationPolicy policy)
    {
        return new NotificationPolicyDto(
            policy.Id.ToString(),
            policy.EventCode,
            policy.EventName,
            policy.Category,
            policy.RecipientStrategy,
            policy.EnableInApp,
            policy.EnableEmail,
            policy.EnableWebhook,
            policy.IsEnabled,
            policy.Remark,
            policy.CreatedAt,
            policy.UpdatedAt);
    }
}
