namespace MiniAdmin.Application.Contracts.Auth;

public interface ILoginSecurityService
{
    Task<CaptchaDto> CreateCaptchaAsync(CancellationToken cancellationToken = default);

    Task ValidateBeforePasswordAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<LoginFailureState> RecordFailureAsync(
        string userName,
        string? clientIp,
        CancellationToken cancellationToken = default);

    Task ClearFailuresAsync(
        string userName,
        string? clientIp,
        CancellationToken cancellationToken = default);

    Task<int?> GetLockRemainingSecondsAsync(
        string userName,
        CancellationToken cancellationToken = default);

    Task UnlockUserAsync(
        string userName,
        CancellationToken cancellationToken = default);
}
