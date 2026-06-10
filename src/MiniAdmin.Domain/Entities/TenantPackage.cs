namespace MiniAdmin.Domain.Entities;

public sealed class TenantPackage
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int MaxUsers { get; set; }

    public int MaxStorageMb { get; set; }

    public string MenuIds { get; set; } = "[]";

    public bool IsEnabled { get; set; } = true;

    public string? Remark { get; set; }
}
