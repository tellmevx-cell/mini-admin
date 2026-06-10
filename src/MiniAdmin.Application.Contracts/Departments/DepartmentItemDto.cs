namespace MiniAdmin.Application.Contracts.Departments;

public sealed record DepartmentItemDto(
    string Id,
    string? ParentId,
    string Code,
    string Name,
    string? Leader,
    string? Phone,
    int Order,
    bool IsEnabled,
    IReadOnlyList<DepartmentItemDto> Children);
