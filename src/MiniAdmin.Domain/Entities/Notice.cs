namespace MiniAdmin.Domain.Entities;

public sealed class Notice
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public bool IsPublished { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
