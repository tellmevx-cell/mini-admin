namespace MiniAdmin.Application.Contracts.PermissionDiagnostics;

public interface IPermissionDiagnosticsAppService
{
    Task<PermissionDiagnosticsDto?> GetByUserNameAsync(
        string userName,
        CancellationToken cancellationToken = default);

    Task<bool> RefreshUserCacheAsync(
        string userName,
        CancellationToken cancellationToken = default);
}
