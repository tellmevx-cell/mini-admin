namespace MiniAdmin.Application.Contracts.Roles;

public sealed record UpdateRoleRequest(
    string Name,
    bool IsEnabled,
    string DataScope = "all",
    Guid[]? CustomDepartmentIds = null);
