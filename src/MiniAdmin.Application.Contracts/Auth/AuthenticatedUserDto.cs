namespace MiniAdmin.Application.Contracts.Auth;

public sealed record AuthenticatedUserDto(
    string UserId,
    Guid? TenantId,
    string UserName,
    string RealName,
    string PasswordHash,
    string SecurityStamp,
    IReadOnlyList<string> RoleCodes,
    IReadOnlyList<string> PermissionCodes);
