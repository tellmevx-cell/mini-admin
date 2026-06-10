namespace MiniAdmin.Application.Contracts.Auth;

public sealed record CaptchaDto(
    string Id,
    string ImageBase64,
    int ExpiresInSeconds);
