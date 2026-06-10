namespace MiniAdmin.Domain.Entities;

public sealed class Customer
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public int IsPublished { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
