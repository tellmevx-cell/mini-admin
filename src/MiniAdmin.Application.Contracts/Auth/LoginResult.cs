namespace MiniAdmin.Application.Contracts.Auth;

public sealed record LoginResult(
    string AccessToken,
    string SessionId,
    string? TenantId,
    string? TenantCode);
