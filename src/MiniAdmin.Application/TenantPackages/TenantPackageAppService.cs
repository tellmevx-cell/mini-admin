using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.TenantPackages;

namespace MiniAdmin.Application.TenantPackages;

public sealed class TenantPackageAppService(
    ITenantPackageRepository tenantPackageRepository) : ITenantPackageAppService
{
    public Task<PageResult<TenantPackageDto>> GetListAsync(
        TenantPackageListQuery query,
        CancellationToken cancellationToken = default)
    {
        return tenantPackageRepository.GetListAsync(query, cancellationToken);
    }

    public Task<IReadOnlyList<TenantPackageOptionDto>> GetOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        return tenantPackageRepository.GetOptionsAsync(cancellationToken);
    }

    public Task<TenantPackageDto> CreateAsync(
        SaveTenantPackageRequest request,
        CancellationToken cancellationToken = default)
    {
        return tenantPackageRepository.CreateAsync(request, cancellationToken);
    }

    public Task<TenantPackageDto?> UpdateAsync(
        Guid id,
        SaveTenantPackageRequest request,
        CancellationToken cancellationToken = default)
    {
        return tenantPackageRepository.UpdateAsync(id, request, cancellationToken);
    }

    public Task<TenantPackageDto?> SetEnabledAsync(
        Guid id,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        return tenantPackageRepository.SetEnabledAsync(id, isEnabled, cancellationToken);
    }

    public Task<IReadOnlyList<string>> GetMenuIdsAsync(
        Guid packageId,
        CancellationToken cancellationToken = default)
    {
        return tenantPackageRepository.GetMenuIdsAsync(packageId, cancellationToken);
    }

    public Task<IReadOnlyList<string>> UpdateMenuIdsAsync(
        Guid packageId,
        IReadOnlyList<Guid> menuIds,
        CancellationToken cancellationToken = default)
    {
        return tenantPackageRepository.UpdateMenuIdsAsync(packageId, menuIds, cancellationToken);
    }
}
