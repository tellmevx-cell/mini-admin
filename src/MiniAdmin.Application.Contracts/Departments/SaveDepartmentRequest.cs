namespace MiniAdmin.Application.Contracts.Departments;

public sealed record SaveDepartmentRequest(
    Guid? ParentId,
    string Code,
    string Name,
    string? Leader,
    string? Phone,
    int Order,
    bool IsEnabled);
