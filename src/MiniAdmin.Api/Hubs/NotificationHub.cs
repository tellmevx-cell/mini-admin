using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MiniAdmin.Application.Contracts.UserNotifications;

namespace MiniAdmin.Api.Hubs;

public interface INotificationHubClient
{
    Task NotificationCreated(IReadOnlyList<UserNotificationDto> notifications, int unreadCount);

    Task UnreadCountChanged(int unreadCount);
}

[Authorize]
public sealed class NotificationHub : Hub<INotificationHubClient>
{
    public object GetConnectionInfo()
    {
        return new
        {
            Context.ConnectionId,
            UserId = Context.UserIdentifier
        };
    }
}
