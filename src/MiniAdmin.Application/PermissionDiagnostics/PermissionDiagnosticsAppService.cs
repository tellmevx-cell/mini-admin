using MiniAdmin.Application.Contracts.PermissionDiagnostics;

namespace MiniAdmin.Application.PermissionDiagnostics;

public sealed class PermissionDiagnosticsAppService(
    IPermissionDiagnosticsRepository repository) : IPermissionDiagnosticsAppService
{
    public Task<PermissionDiagnosticsDto?> GetByUserNameAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        return repository.GetByUserNameAsync(userName, cancellationToken);
    }

    public Task<bool> RefreshUserCacheAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        return repository.RefreshUserCacheAsync(userName, cancellationToken);
    }
}
