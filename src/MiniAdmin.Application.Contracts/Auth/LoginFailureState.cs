namespace MiniAdmin.Application.Contracts.Auth;

public sealed record LoginFailureState(
    bool CaptchaRequired,
    int? LockRemainingSeconds);
