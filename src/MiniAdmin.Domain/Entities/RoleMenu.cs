namespace MiniAdmin.Domain.Entities;

public sealed class RoleMenu
{
    public Guid RoleId { get; set; }

    public Role Role { get; set; } = null!;

    public Guid MenuId { get; set; }

    public Menu Menu { get; set; } = null!;
}
