namespace MiniAdmin.Domain.Entities;

public sealed class AbacPolicy
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public string Name { get; set; } = string.Empty;

    public string SubjectType { get; set; } = "Any";

    public string? SubjectId { get; set; }

    public string Resource { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string Effect { get; set; } = "Allow";

    public string ConditionsJson { get; set; } = string.Empty;

    public int Priority { get; set; }

    public bool IsEnabled { get; set; } = true;

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
