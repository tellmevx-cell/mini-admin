using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Caching;
using MiniAdmin.Application.Contracts.Menus;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfMenuRepository(
    MiniAdminDbContext dbContext,
    IUserAuthorizationCache userAuthorizationCache,
    ICurrentTenant currentTenant) : IMenuRepository
{
    public async Task<IReadOnlyList<string>> GetPermissionCodesByUserNameAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        return await userAuthorizationCache.GetPermissionCodesAsync(
            userName,
            async token =>
            {
                var menus = await GetAuthorizedMenusAsync(userName, token);

                return menus
                    .Where(x => !string.IsNullOrWhiteSpace(x.PermissionCode))
                    .Select(x => x.PermissionCode!)
                    .Distinct()
                    .Order()
                    .ToArray();
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<VbenMenuDto>> GetMenusByUserNameAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        return await userAuthorizationCache.GetMenusAsync(
            userName,
            async token =>
            {
                var menus = await GetAuthorizedMenusAsync(userName, token);
                var menuLookup = menus
                    .Where(x => x.IsVisible)
                    .OrderBy(x => x.Order)
                    .ThenBy(x => x.Name)
                    .ToLookup(x => x.ParentId);

                return menuLookup[null]
                    .Select(menu => ToVbenMenu(menu, menuLookup))
                    .ToArray();
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<MenuTreeNodeDto>> GetManagementTreeAsync(
        CancellationToken cancellationToken = default)
    {
        var menusQuery = dbContext.Menus
            .AsNoTracking()
            .Where(x => x.IsEnabled);
        var packageMenuIds = await GetCurrentTenantPackageMenuIdsAsync(cancellationToken);
        if (packageMenuIds is not null)
        {
            menusQuery = menusQuery.Where(x => packageMenuIds.Contains(x.Id));
        }

        var menus = await menusQuery
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        var menuLookup = menus.ToLookup(x => x.ParentId);

        return menuLookup[null]
            .Select(menu => ToMenuTreeNode(menu, menuLookup))
            .ToArray();
    }

    public async Task<IReadOnlyList<MenuManagementItemDto>> GetManagementListAsync(
        CancellationToken cancellationToken = default)
    {
        var menus = await dbContext.Menus
            .AsNoTracking()
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        var menuLookup = menus.ToLookup(x => x.ParentId);

        return menuLookup[null]
            .Select(menu => ToManagementItem(menu, menuLookup))
            .ToArray();
    }

    public async Task<MenuManagementItemDto> CreateAsync(
        SaveMenuRequest request,
        CancellationToken cancellationToken = default)
    {
        var menu = new Menu
        {
            Id = Guid.NewGuid()
        };

        ApplyRequest(menu, request);
        dbContext.Menus.Add(menu);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToManagementItem(menu, EmptyMenuLookup);
    }

    public async Task<MenuManagementItemDto?> UpdateAsync(
        Guid id,
        SaveMenuRequest request,
        CancellationToken cancellationToken = default)
    {
        var menu = await dbContext.Menus.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (menu is null)
        {
            return null;
        }

        ApplyRequest(menu, request);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToManagementItem(menu, EmptyMenuLookup);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var hasChildren = await dbContext.Menus.AnyAsync(x => x.ParentId == id, cancellationToken);
        if (hasChildren)
        {
            return false;
        }

        var isAssignedToRole = await dbContext.RoleMenus.AnyAsync(x => x.MenuId == id, cancellationToken);
        if (isAssignedToRole)
        {
            return false;
        }

        var menu = await dbContext.Menus.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (menu is null)
        {
            return false;
        }

        dbContext.Menus.Remove(menu);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<List<Menu>> GetAuthorizedMenusAsync(
        string userName,
        CancellationToken cancellationToken)
    {
        var roleIds = await dbContext.UserRoles
            .AsNoTracking()
            .Where(x => x.User.UserName == userName && x.User.IsEnabled && x.Role.IsEnabled)
            .Select(x => x.RoleId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var menuIds = (await dbContext.RoleMenus
            .AsNoTracking()
            .Where(x => roleIds.Contains(x.RoleId) && x.Menu.IsEnabled)
            .Select(x => x.MenuId)
            .Distinct()
            .ToListAsync(cancellationToken))
            .ToHashSet();
        var packageMenuIds = await GetCurrentTenantPackageMenuIdsAsync(cancellationToken);
        if (packageMenuIds is not null)
        {
            menuIds.IntersectWith(packageMenuIds);
        }

        await AddAncestorMenuIdsAsync(menuIds, cancellationToken);

        return await dbContext.Menus
            .AsNoTracking()
            .Where(x => menuIds.Contains(x.Id) && x.IsEnabled)
            .ToListAsync(cancellationToken);
    }

    private async Task<HashSet<Guid>?> GetCurrentTenantPackageMenuIdsAsync(
        CancellationToken cancellationToken)
    {
        if (currentTenant.IsPlatform || !currentTenant.TenantId.HasValue)
        {
            return null;
        }

        var menuIdsJson = await dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.Id == currentTenant.TenantId.Value)
            .Select(x => x.Package == null ? null : x.Package.MenuIds)
            .SingleOrDefaultAsync(cancellationToken);

        return menuIdsJson is null
            ? null
            : EfTenantPackageRepository.ParseMenuIds(menuIdsJson);
    }

    private async Task AddAncestorMenuIdsAsync(
        HashSet<Guid> menuIds,
        CancellationToken cancellationToken)
    {
        var parentIds = await dbContext.Menus
            .AsNoTracking()
            .Where(x => menuIds.Contains(x.Id) && x.ParentId.HasValue && x.IsEnabled)
            .Select(x => x.ParentId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var missingParentIds = parentIds
            .Where(parentId => !menuIds.Contains(parentId))
            .ToHashSet();

        while (missingParentIds.Count > 0)
        {
            var parentMenus = await dbContext.Menus
                .AsNoTracking()
                .Where(x => missingParentIds.Contains(x.Id) && x.IsEnabled)
                .ToListAsync(cancellationToken);

            missingParentIds.Clear();
            foreach (var parentMenu in parentMenus)
            {
                if (!menuIds.Add(parentMenu.Id))
                {
                    continue;
                }

                if (parentMenu.ParentId is Guid grandParentId && !menuIds.Contains(grandParentId))
                {
                    missingParentIds.Add(grandParentId);
                }
            }
        }
    }

    private static VbenMenuDto ToVbenMenu(Menu menu, ILookup<Guid?, Menu> menuLookup)
    {
        var children = menuLookup[menu.Id]
            .Select(child => ToVbenMenu(child, menuLookup))
            .ToArray();

        return new VbenMenuDto(
            menu.Name,
            menu.Path,
            menu.Component,
            menu.Redirect,
            new VbenMenuMetaDto(
                menu.Title,
                menu.Icon,
                menu.Order,
                menu.AffixTab ? true : null),
            children);
    }

    private static MenuTreeNodeDto ToMenuTreeNode(Menu menu, ILookup<Guid?, Menu> menuLookup)
    {
        var children = menuLookup[menu.Id]
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Name)
            .Select(child => ToMenuTreeNode(child, menuLookup))
            .ToArray();

        return new MenuTreeNodeDto(
            menu.Id.ToString(),
            menu.Name,
            menu.Title,
            children);
    }

    private static MenuManagementItemDto ToManagementItem(Menu menu, ILookup<Guid?, Menu> menuLookup)
    {
        var children = menuLookup[menu.Id]
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Name)
            .Select(child => ToManagementItem(child, menuLookup))
            .ToArray();

        return new MenuManagementItemDto(
            menu.Id.ToString(),
            menu.ParentId?.ToString(),
            menu.Name,
            menu.Path,
            menu.Component,
            menu.Redirect,
            menu.Title,
            menu.Icon,
            menu.Order,
            menu.AffixTab,
            menu.PermissionCode,
            menu.IsEnabled,
            menu.IsVisible,
            children);
    }

    private static void ApplyRequest(Menu menu, SaveMenuRequest request)
    {
        menu.ParentId = request.ParentId;
        menu.Name = request.Name.Trim();
        menu.Path = request.Path.Trim();
        menu.Component = NormalizeOptional(request.Component);
        menu.Redirect = NormalizeOptional(request.Redirect);
        menu.Title = request.Title.Trim();
        menu.Icon = NormalizeOptional(request.Icon);
        menu.Order = request.Order;
        menu.AffixTab = request.AffixTab;
        menu.PermissionCode = NormalizeOptional(request.PermissionCode);
        menu.IsEnabled = request.IsEnabled;
        menu.IsVisible = request.IsVisible;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static readonly ILookup<Guid?, Menu> EmptyMenuLookup = Array.Empty<Menu>().ToLookup(x => x.ParentId);
}
