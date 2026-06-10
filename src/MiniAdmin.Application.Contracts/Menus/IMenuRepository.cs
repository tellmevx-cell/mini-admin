namespace MiniAdmin.Application.Contracts.Menus;

public interface IMenuRepository
{
    Task<IReadOnlyList<string>> GetPermissionCodesByUserNameAsync(string userName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VbenMenuDto>> GetMenusByUserNameAsync(string userName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MenuTreeNodeDto>> GetManagementTreeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MenuManagementItemDto>> GetManagementListAsync(CancellationToken cancellationToken = default);

    Task<MenuManagementItemDto> CreateAsync(SaveMenuRequest request, CancellationToken cancellationToken = default);

    Task<MenuManagementItemDto?> UpdateAsync(Guid id, SaveMenuRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
