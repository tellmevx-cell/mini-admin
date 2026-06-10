namespace MiniAdmin.Infrastructure.Notifications;

public interface IEmailNotificationSender
{
    Task SendAsync(
        string recipientEmail,
        string title,
        string content,
        CancellationToken cancellationToken = default);
}
