namespace MiniAdmin.Domain.Entities;

public sealed class Department
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public Guid? ParentId { get; set; }

    public Department? Parent { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Leader { get; set; }

    public string? Phone { get; set; }

    public int Order { get; set; }

    public bool IsEnabled { get; set; } = true;

    public List<Department> Children { get; set; } = [];
}
