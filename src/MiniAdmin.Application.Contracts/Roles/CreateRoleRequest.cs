namespace MiniAdmin.Application.Contracts.Roles;

public sealed record CreateRoleRequest(
    string Code,
    string Name,
    bool IsEnabled,
    string DataScope = "all",
    Guid[]? CustomDepartmentIds = null);
