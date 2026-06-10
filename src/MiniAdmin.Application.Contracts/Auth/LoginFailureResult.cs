namespace MiniAdmin.Application.Contracts.Auth;

public sealed record LoginFailureResult(
    bool CaptchaRequired,
    int? LockRemainingSeconds);
