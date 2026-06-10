using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Roles;

namespace MiniAdmin.Application.Roles;

public sealed class RoleAppService(IRoleRepository roleRepository) : IRoleAppService
{
    public Task<PageResult<RoleListItemDto>> GetListAsync(
        RoleListQuery query,
        CancellationToken cancellationToken = default)
    {
        return roleRepository.GetListAsync(query, cancellationToken);
    }

    public Task<RoleListItemDto> CreateAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        return roleRepository.CreateAsync(request, cancellationToken);
    }

    public Task<RoleListItemDto?> UpdateAsync(
        Guid id,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        return roleRepository.UpdateAsync(id, request, cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return roleRepository.DeleteAsync(id, cancellationToken);
    }

    public Task<IReadOnlyList<string>> GetMenuIdsAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        return roleRepository.GetMenuIdsAsync(roleId, cancellationToken);
    }

    public Task<IReadOnlyList<string>> UpdateMenuIdsAsync(
        Guid roleId,
        UpdateRoleMenusRequest request,
        CancellationToken cancellationToken = default)
    {
        return roleRepository.UpdateMenuIdsAsync(roleId, request.MenuIds, cancellationToken);
    }
}
