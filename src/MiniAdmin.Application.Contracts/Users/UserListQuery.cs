namespace MiniAdmin.Application.Contracts.Users;

public sealed record UserListQuery(
    int Page = 1,
    int PageSize = 20,
    string? UserName = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null,
    string? CurrentUserName = null);
