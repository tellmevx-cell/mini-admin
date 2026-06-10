namespace MiniAdmin.Domain.Entities;

public sealed class SecurityEvent
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Level { get; set; } = "Info";

    public Guid? UserId { get; set; }

    public string? UserName { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? RelatedEntityType { get; set; }

    public string? RelatedEntityId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
