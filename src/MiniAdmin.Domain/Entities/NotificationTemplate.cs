namespace MiniAdmin.Domain.Entities;

public sealed class NotificationTemplate
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public string? Channel { get; set; }

    public string TitleTemplate { get; set; } = string.Empty;

    public string MessageTemplate { get; set; } = string.Empty;

    public string? LinkTemplate { get; set; }

    public bool IsEnabled { get; set; } = true;

    public string? Remark { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
