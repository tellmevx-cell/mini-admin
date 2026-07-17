using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.Tenants;

public interface ITenantAppService
{
    Task<PageResult<TenantDto>> GetListAsync(
        TenantListQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantLoginOptionDto>> GetLoginOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantInitializationTemplateDto>> GetInitializationTemplatesAsync(
        CancellationToken cancellationToken = default);

    Task<TenantDto> CreateAsync(
        CreateTenantRequest request,
        CancellationToken cancellationToken = default);

    Task<TenantDto?> UpdateAsync(
        Guid id,
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default);

    Task<TenantDto?> EnableAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TenantDto?> DisableAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TenantDto?> RenewAsync(
        Guid id,
        RenewTenantRequest request,
        CancellationToken cancellationToken = default);

    Task<PageResult<TenantLifecycleRecordDto>> GetLifecycleRecordsAsync(
        Guid id,
        TenantLifecycleRecordListQuery query,
        CancellationToken cancellationToken = default);
}
