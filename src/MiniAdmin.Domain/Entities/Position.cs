namespace MiniAdmin.Domain.Entities;

public sealed class Position
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Order { get; set; }

    public string? Remark { get; set; }

    public bool IsEnabled { get; set; } = true;
}
