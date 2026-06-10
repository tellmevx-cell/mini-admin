namespace MiniAdmin.Application.Contracts.Auth;

public interface ITokenService
{
    string CreateAccessToken(
        string userId,
        string userName,
        string sessionId,
        string? tenantId,
        string? tenantCode,
        string securityStamp,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> permissionCodes);
}
