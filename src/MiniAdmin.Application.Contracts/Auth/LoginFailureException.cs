namespace MiniAdmin.Application.Contracts.Auth;

public sealed class LoginFailureException(
    string message,
    bool captchaRequired,
    int? lockRemainingSeconds = null) : Exception(message)
{
    public bool CaptchaRequired { get; } = captchaRequired;

    public int? LockRemainingSeconds { get; } = lockRemainingSeconds;
}
