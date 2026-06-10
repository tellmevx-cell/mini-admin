namespace MiniAdmin.Application.Contracts.Menus;

public sealed record VbenMenuDto(
    string Name,
    string Path,
    string? Component,
    string? Redirect,
    VbenMenuMetaDto Meta,
    IReadOnlyList<VbenMenuDto> Children);

public sealed record VbenMenuMetaDto(
    string Title,
    string? Icon = null,
    int? Order = null,
    bool? AffixTab = null);
