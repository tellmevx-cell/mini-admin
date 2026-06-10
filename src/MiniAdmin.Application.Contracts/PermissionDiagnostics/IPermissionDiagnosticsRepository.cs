namespace MiniAdmin.Application.Contracts.PermissionDiagnostics;

public interface IPermissionDiagnosticsRepository
{
    Task<PermissionDiagnosticsDto?> GetByUserNameAsync(
        string userName,
        CancellationToken cancellationToken = default);

    Task<bool> RefreshUserCacheAsync(
        string userName,
        CancellationToken cancellationToken = default);
}
