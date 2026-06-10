namespace MiniAdmin.Infrastructure.Notifications;

public interface IWebhookNotificationSender
{
    Task SendAsync(
        string endpointUrl,
        string payloadJson,
        string? secret,
        CancellationToken cancellationToken = default);
}
