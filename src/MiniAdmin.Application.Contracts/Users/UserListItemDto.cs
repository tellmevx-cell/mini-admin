namespace MiniAdmin.Application.Contracts.Users;

public sealed record UserListItemDto(
    string Id,
    string UserName,
    string RealName,
    string? Email,
    string? DepartmentId,
    string? DepartmentName,
    string? PositionId,
    string? PositionName,
    IReadOnlyList<string> Roles,
    int Status,
    int? LoginLockRemainingSeconds = null);
