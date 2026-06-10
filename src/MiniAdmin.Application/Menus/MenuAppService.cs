using MiniAdmin.Application.Contracts.Menus;

namespace MiniAdmin.Application.Menus;

public sealed class MenuAppService(IMenuRepository menuRepository) : IMenuAppService
{
    public Task<IReadOnlyList<VbenMenuDto>> GetAllMenusAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        return menuRepository.GetMenusByUserNameAsync(userName, cancellationToken);
    }

    public Task<IReadOnlyList<MenuTreeNodeDto>> GetManagementTreeAsync(
        CancellationToken cancellationToken = default)
    {
        return menuRepository.GetManagementTreeAsync(cancellationToken);
    }

    public Task<IReadOnlyList<MenuManagementItemDto>> GetManagementListAsync(
        CancellationToken cancellationToken = default)
    {
        return menuRepository.GetManagementListAsync(cancellationToken);
    }

    public Task<MenuManagementItemDto> CreateAsync(
        SaveMenuRequest request,
        CancellationToken cancellationToken = default)
    {
        return menuRepository.CreateAsync(request, cancellationToken);
    }

    public Task<MenuManagementItemDto?> UpdateAsync(
        Guid id,
        SaveMenuRequest request,
        CancellationToken cancellationToken = default)
    {
        return menuRepository.UpdateAsync(id, request, cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return menuRepository.DeleteAsync(id, cancellationToken);
    }
}
