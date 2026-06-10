using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.UserNotifications;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfNotificationSubscriptionRepository(MiniAdminDbContext dbContext)
    : INotificationSubscriptionRepository
{
    public async Task<NotificationSubscriptionListResult> GetMyAsync(
        Guid userId,
        NotificationSubscriptionListQuery query,
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

        var policies = await policiesQuery
            .OrderBy(policy => policy.Category)
            .ThenBy(policy => policy.EventCode)
            .ToArrayAsync(cancellationToken);
        var eventCodes = policies.Select(policy => policy.EventCode).ToArray();
        var subscriptions = await dbContext.NotificationSubscriptions
            .AsNoTracking()
            .Where(subscription => subscription.UserId == userId)
            .ToArrayAsync(cancellationToken);
        var subscriptionMap = subscriptions
            .Where(subscription => eventCodes.Contains(subscription.EventCode, StringComparer.Ordinal))
            .ToDictionary(subscription => subscription.EventCode, StringComparer.Ordinal);

        var items = policies
            .Select(policy =>
            {
                subscriptionMap.TryGetValue(policy.EventCode, out var subscription);
                return ToDto(policy, subscription);
            })
            .ToArray();

        return new NotificationSubscriptionListResult(items, items.Length);
    }

    public async Task<NotificationSubscriptionDto?> SaveMyAsync(
        Guid userId,
        string eventCode,
        SaveNotificationSubscriptionRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var normalizedEventCode = eventCode.Trim();
        var policy = await dbContext.NotificationPolicies
            .SingleOrDefaultAsync(item => item.EventCode == normalizedEventCode, cancellationToken);
        if (policy is null)
        {
            return null;
        }

        var subscription = await dbContext.NotificationSubscriptions.SingleOrDefaultAsync(
            item => item.UserId == userId && item.EventCode == normalizedEventCode,
            cancellationToken);
        if (subscription is null)
        {
            subscription = new NotificationSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EventCode = normalizedEventCode,
                CreatedAt = now
            };
            dbContext.NotificationSubscriptions.Add(subscription);
        }

        subscription.EnableInApp = request.EnableInApp;
        subscription.EnableEmail = request.EnableEmail;
        subscription.EnableWebhook = request.EnableWebhook;
        subscription.IsEnabled = request.IsEnabled;
        subscription.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(policy, subscription);
    }

    public async Task<bool> ResetMyAsync(
        Guid userId,
        string eventCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedEventCode = eventCode.Trim();
        var subscription = await dbContext.NotificationSubscriptions.SingleOrDefaultAsync(
            item => item.UserId == userId && item.EventCode == normalizedEventCode,
            cancellationToken);
        if (subscription is null)
        {
            return false;
        }

        dbContext.NotificationSubscriptions.Remove(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> ResetAllMyAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var subscriptions = await dbContext.NotificationSubscriptions
            .Where(item => item.UserId == userId)
            .ToArrayAsync(cancellationToken);
        if (subscriptions.Length == 0)
        {
            return 0;
        }

        dbContext.NotificationSubscriptions.RemoveRange(subscriptions);
        await dbContext.SaveChangesAsync(cancellationToken);
        return subscriptions.Length;
    }

    private static NotificationSubscriptionDto ToDto(
        NotificationPolicy policy,
        NotificationSubscription? subscription)
    {
        var policyEnableInApp = policy.IsEnabled && policy.EnableInApp;
        var policyEnableEmail = policy.IsEnabled && policy.EnableEmail;
        var policyEnableWebhook = policy.IsEnabled && policy.EnableWebhook;
        var hasCustomPreference = subscription is not null;

        return new NotificationSubscriptionDto(
            subscription?.Id.ToString(),
            policy.EventCode,
            policy.EventName,
            policy.Category,
            policyEnableInApp,
            policyEnableEmail,
            policyEnableWebhook,
            subscription?.EnableInApp ?? policyEnableInApp,
            subscription?.EnableEmail ?? policyEnableEmail,
            subscription?.EnableWebhook ?? policyEnableWebhook,
            subscription?.IsEnabled ?? policy.IsEnabled,
            hasCustomPreference,
            subscription?.UpdatedAt);
    }
}
