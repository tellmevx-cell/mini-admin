namespace MiniAdmin.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string RealName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    public string? Email { get; set; }

    public Guid? DepartmentId { get; set; }

    public Department? Department { get; set; }

    public Guid? PositionId { get; set; }

    public Position? Position { get; set; }

    public bool IsEnabled { get; set; } = true;

    public List<UserRole> UserRoles { get; set; } = [];
}
