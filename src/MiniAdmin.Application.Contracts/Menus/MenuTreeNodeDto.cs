namespace MiniAdmin.Application.Contracts.Menus;

public sealed record MenuTreeNodeDto(
    string Id,
    string Name,
    string Title,
    IReadOnlyList<MenuTreeNodeDto> Children);
