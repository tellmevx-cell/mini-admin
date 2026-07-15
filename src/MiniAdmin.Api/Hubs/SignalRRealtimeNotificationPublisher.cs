using Microsoft.AspNetCore.SignalR;
using MiniAdmin.Application.Contracts.UserNotifications;

namespace MiniAdmin.Api.Hubs;

public sealed class SignalRRealtimeNotificationPublisher(
    IHubContext<NotificationHub, INotificationHubClient> hubContext,
    ILogger<SignalRRealtimeNotificationPublisher> logger) : IRealtimeNotificationPublisher
{
    public async Task PublishCreatedAsync(
        Guid userId,
        IReadOnlyList<UserNotificationDto> notifications,
        int unreadCount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await hubContext.Clients.User(userId.ToString()).NotificationCreated(
                notifications,
                unreadCount);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "向用户 {UserId} 推送实时通知失败。", userId);
        }
    }

    public async Task PublishUnreadCountAsync(
        Guid userId,
        int unreadCount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await hubContext.Clients.User(userId.ToString()).UnreadCountChanged(unreadCount);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "向用户 {UserId} 推送未读数量失败。", userId);
        }
    }
}
