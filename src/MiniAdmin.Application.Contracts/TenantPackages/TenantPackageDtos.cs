using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.TenantPackages;

public sealed record TenantPackageListQuery(
    int Page = 1,
    int PageSize = 10,
    string? Name = null,
    bool? IsEnabled = null);

public sealed record TenantPackageDto(
    string Id,
    string Name,
    int MaxUsers,
    int MaxStorageMb,
    int MenuCount,
    bool IsEnabled,
    string? Remark);

public sealed record TenantPackageOptionDto(
    string Id,
    string Name,
    bool IsEnabled);

public sealed record SaveTenantPackageRequest(
    string Name,
    int MaxUsers,
    int MaxStorageMb,
    bool IsEnabled = true,
    string? Remark = null);

public sealed record UpdateTenantPackageMenusRequest(IReadOnlyList<Guid> MenuIds);

public interface ITenantPackageAppService
{
    Task<PageResult<TenantPackageDto>> GetListAsync(
        TenantPackageListQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantPackageOptionDto>> GetOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<TenantPackageDto> CreateAsync(
        SaveTenantPackageRequest request,
        CancellationToken cancellationToken = default);

    Task<TenantPackageDto?> UpdateAsync(
        Guid id,
        SaveTenantPackageRequest request,
        CancellationToken cancellationToken = default);

    Task<TenantPackageDto?> SetEnabledAsync(
        Guid id,
        bool isEnabled,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetMenuIdsAsync(
        Guid packageId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> UpdateMenuIdsAsync(
        Guid packageId,
        IReadOnlyList<Guid> menuIds,
        CancellationToken cancellationToken = default);
}

public interface ITenantPackageRepository
{
    Task<PageResult<TenantPackageDto>> GetListAsync(
        TenantPackageListQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantPackageOptionDto>> GetOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<TenantPackageDto> CreateAsync(
        SaveTenantPackageRequest request,
        CancellationToken cancellationToken = default);

    Task<TenantPackageDto?> UpdateAsync(
        Guid id,
        SaveTenantPackageRequest request,
        CancellationToken cancellationToken = default);

    Task<TenantPackageDto?> SetEnabledAsync(
        Guid id,
        bool isEnabled,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetMenuIdsAsync(
        Guid packageId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> UpdateMenuIdsAsync(
        Guid packageId,
        IReadOnlyList<Guid> menuIds,
        CancellationToken cancellationToken = default);
}
