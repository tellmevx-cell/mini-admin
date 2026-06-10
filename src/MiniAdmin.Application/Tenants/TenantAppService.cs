using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.Tenants;

namespace MiniAdmin.Application.Tenants;

public sealed class TenantAppService(ITenantRepository tenantRepository) : ITenantAppService
{
    public Task<PageResult<TenantDto>> GetListAsync(
        TenantListQuery query,
        CancellationToken cancellationToken = default)
    {
        return tenantRepository.GetListAsync(query, cancellationToken);
    }

    public Task<IReadOnlyList<TenantLoginOptionDto>> GetLoginOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        return tenantRepository.GetLoginOptionsAsync(cancellationToken);
    }

    public Task<IReadOnlyList<TenantInitializationTemplateDto>> GetInitializationTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        return tenantRepository.GetInitializationTemplatesAsync(cancellationToken);
    }

    public Task<TenantDto> CreateAsync(
        CreateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        return tenantRepository.CreateAsync(request, cancellationToken);
    }

    public Task<TenantDto?> UpdateAsync(
        Guid id,
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        return tenantRepository.UpdateAsync(id, request, cancellationToken);
    }

    public Task<TenantDto?> EnableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return tenantRepository.SetStatusAsync(id, "Active", cancellationToken);
    }

    public Task<TenantDto?> DisableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return tenantRepository.SetStatusAsync(id, "Disabled", cancellationToken);
    }
}
