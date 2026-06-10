namespace MiniAdmin.Domain.Entities;

public sealed class NotificationSubscription
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public string EventCode { get; set; } = string.Empty;

    public bool EnableInApp { get; set; } = true;

    public bool EnableEmail { get; set; }

    public bool EnableWebhook { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
