namespace MiniAdmin.Application.Contracts.Roles;

public sealed record RoleListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Code = null,
    string? Name = null);
