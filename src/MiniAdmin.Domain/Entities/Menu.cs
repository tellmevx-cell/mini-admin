namespace MiniAdmin.Domain.Entities;

public sealed class Menu
{
    public Guid Id { get; set; }

    public Guid? ParentId { get; set; }

    public Menu? Parent { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string? Component { get; set; }

    public string? Redirect { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Icon { get; set; }

    public int Order { get; set; }

    public bool AffixTab { get; set; }

    public string? PermissionCode { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool IsVisible { get; set; } = true;

    public List<Menu> Children { get; set; } = [];

    public List<RoleMenu> RoleMenus { get; set; } = [];
}
