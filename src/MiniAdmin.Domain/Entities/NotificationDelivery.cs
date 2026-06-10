namespace MiniAdmin.Domain.Entities;

public sealed class NotificationDelivery
{
    public Guid Id { get; set; }

    public string Channel { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public string RecipientAddress { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string SourceType { get; set; } = string.Empty;

    public string SourceId { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? SentAt { get; set; }
}
