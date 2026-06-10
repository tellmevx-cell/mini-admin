namespace MiniAdmin.Infrastructure.Notifications;

public sealed class WebhookNotificationOptions
{
    public bool Enabled { get; set; }

    public string EndpointUrl { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;
}
