namespace MiniAdmin.Application.Contracts.Roles;

public sealed record RoleListItemDto(
    string Id,
    string Code,
    string Name,
    string DataScope,
    int Status,
    IReadOnlyList<string>? CustomDepartmentIds = null);
