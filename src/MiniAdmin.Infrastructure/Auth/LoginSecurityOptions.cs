namespace MiniAdmin.Infrastructure.Auth;

public sealed class LoginSecurityOptions
{
    public int CaptchaRequiredFailures { get; set; } = 3;

    public int LockoutFailures { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 10;

    public int CaptchaExpireSeconds { get; set; } = 120;
}
