using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Tenants;

namespace MiniAdmin.Application.Contracts.MultiTenancy;

public interface ITenantRepository
{
    Task<TenantLookupDto?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<TenantLookupDto?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PageResult<TenantDto>> GetListAsync(
        TenantListQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantLoginOptionDto>> GetLoginOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantInitializationTemplateDto>> GetInitializationTemplatesAsync(
        CancellationToken cancellationToken = default);

    Task<TenantDto> CreateAsync(
        CreateTenantRequest request,
        TenantOperationActor actor,
        CancellationToken cancellationToken = default);

    Task<TenantDto?> UpdateAsync(
        Guid id,
        UpdateTenantRequest request,
        TenantOperationActor actor,
        CancellationToken cancellationToken = default);

    Task<TenantDto?> SetStatusAsync(
        Guid id,
        string status,
        TenantOperationActor actor,
        CancellationToken cancellationToken = default);

    Task<TenantDto?> RenewAsync(
        Guid id,
        RenewTenantRequest request,
        TenantOperationActor actor,
        CancellationToken cancellationToken = default);
}
