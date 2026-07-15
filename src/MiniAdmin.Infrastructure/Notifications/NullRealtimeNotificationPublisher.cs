using MiniAdmin.Application.Contracts.UserNotifications;

namespace MiniAdmin.Infrastructure.Notifications;

public sealed class NullRealtimeNotificationPublisher : IRealtimeNotificationPublisher
{
    public Task PublishCreatedAsync(
        Guid userId,
        IReadOnlyList<UserNotificationDto> notifications,
        int unreadCount,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task PublishUnreadCountAsync(
        Guid userId,
        int unreadCount,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
