using MiniAdmin.Application.Contracts.UserNotifications;

namespace MiniAdmin.Application.UserNotifications;

public sealed class NotificationSubscriptionAppService(
    INotificationSubscriptionRepository notificationSubscriptionRepository) : INotificationSubscriptionAppService
{
    public Task<NotificationSubscriptionListResult> GetMyAsync(
        Guid userId,
        NotificationSubscriptionListQuery query,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException("用户标识不能为空。");
        }

        return notificationSubscriptionRepository.GetMyAsync(userId, query, cancellationToken);
    }

    public Task<NotificationSubscriptionDto?> SaveMyAsync(
        Guid userId,
        string eventCode,
        SaveNotificationSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException("用户标识不能为空。");
        }

        if (string.IsNullOrWhiteSpace(eventCode))
        {
            throw new InvalidOperationException("事件编码不能为空。");
        }

        return notificationSubscriptionRepository.SaveMyAsync(
            userId,
            eventCode,
            request,
            DateTimeOffset.UtcNow,
            cancellationToken);
    }

    public Task<bool> ResetMyAsync(
        Guid userId,
        string eventCode,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException("用户标识不能为空。");
        }

        if (string.IsNullOrWhiteSpace(eventCode))
        {
            throw new InvalidOperationException("事件编码不能为空。");
        }

        return notificationSubscriptionRepository.ResetMyAsync(userId, eventCode, cancellationToken);
    }

    public Task<int> ResetAllMyAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException("用户标识不能为空。");
        }

        return notificationSubscriptionRepository.ResetAllMyAsync(userId, cancellationToken);
    }
}
