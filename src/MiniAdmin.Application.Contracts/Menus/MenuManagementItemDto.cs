namespace MiniAdmin.Application.Contracts.Menus;

public sealed record MenuManagementItemDto(
    string Id,
    string? ParentId,
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
    bool IsVisible,
    IReadOnlyList<MenuManagementItemDto> Children);
