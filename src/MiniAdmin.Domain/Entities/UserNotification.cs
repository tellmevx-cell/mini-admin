namespace MiniAdmin.Domain.Entities;

public sealed class UserNotification
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public string? Link { get; set; }

    public string SourceType { get; set; } = string.Empty;

    public string SourceId { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ReadAt { get; set; }
}
