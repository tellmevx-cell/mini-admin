namespace MiniAdmin.Application.Contracts.Auth;

public sealed record LoginRequest(
    string Username,
    string Password,
    string? TenantCode = null,
    string? ClientIp = null,
    string? CaptchaId = null,
    string? CaptchaCode = null);
