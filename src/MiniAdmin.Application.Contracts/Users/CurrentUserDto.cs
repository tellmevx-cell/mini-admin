namespace MiniAdmin.Application.Contracts.Users;

public sealed record CurrentUserDto(
    string UserId,
    string Username,
    string RealName,
    string? DepartmentId,
    string? DepartmentName,
    string? PositionId,
    string? PositionName,
    IReadOnlyList<string> Roles);
