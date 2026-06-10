using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.Roles;

public interface IRoleRepository
{
    Task<PageResult<RoleListItemDto>> GetListAsync(
        RoleListQuery query,
        CancellationToken cancellationToken = default);

    Task<RoleListItemDto> CreateAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken = default);

    Task<RoleListItemDto?> UpdateAsync(
        Guid id,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetMenuIdsAsync(
        Guid roleId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> UpdateMenuIdsAsync(
        Guid roleId,
        IReadOnlyList<Guid> menuIds,
        CancellationToken cancellationToken = default);
}
