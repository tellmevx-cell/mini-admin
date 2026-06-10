namespace MiniAdmin.Domain.Entities;

public sealed class Role
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string DataScope { get; set; } = "all";

    public string? CustomDepartmentIds { get; set; }

    public bool IsEnabled { get; set; } = true;

    public List<UserRole> UserRoles { get; set; } = [];

    public List<RoleMenu> RoleMenus { get; set; } = [];
}
