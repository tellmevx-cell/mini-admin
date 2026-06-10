namespace MiniAdmin.Application.Contracts.Menus;

public sealed record SaveMenuRequest(
    Guid? ParentId,
    string Name,
    string Path,
    string? Component,
    string? Redirect,
    string Title,
    string? Icon,
    int Order,
    bool AffixTab,
    string? PermissionCode,
    bool IsEnabled,
    bool IsVisible);
