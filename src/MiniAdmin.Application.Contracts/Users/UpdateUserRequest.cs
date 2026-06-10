namespace MiniAdmin.Application.Contracts.Users;

public sealed record UpdateUserRequest(
    string RealName,
    string? Email,
    string? Password,
    Guid? DepartmentId,
    Guid? PositionId,
    IReadOnlyList<Guid> RoleIds,
    bool IsEnabled);
