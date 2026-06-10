namespace MiniAdmin.Application.Contracts.Roles;

public sealed record UpdateRoleMenusRequest(IReadOnlyList<Guid> MenuIds);
