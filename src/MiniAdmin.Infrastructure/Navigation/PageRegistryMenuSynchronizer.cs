using Microsoft.EntityFrameworkCore;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Platform.Navigation;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Infrastructure.Navigation;

public interface IPageRegistryMenuSynchronizer
{
    Task SynchronizeAsync(CancellationToken cancellationToken = default);
}

public sealed class PageRegistryMenuSynchronizer(
    MiniAdminDbContext dbContext,
    IPageRegistry pageRegistry) : IPageRegistryMenuSynchronizer
{
    public async Task SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        var pageIds = pageRegistry.Pages.ToDictionary(
            page => page.Key,
            PageIdentity.ForPage,
            StringComparer.OrdinalIgnoreCase);
        var adminMenuIds = await dbContext.RoleMenus
            .Where(roleMenu => roleMenu.RoleId == MiniAdminSeedIds.AdminRoleId)
            .Select(roleMenu => roleMenu.MenuId)
            .ToHashSetAsync(cancellationToken);

        foreach (var page in pageRegistry.Pages)
        {
            var pageId = pageIds[page.Key];
            var parentId = string.IsNullOrWhiteSpace(page.ParentKey)
                ? page.ParentId
                : pageIds[page.ParentKey];
            var accessPermission = page.Permissions.FirstOrDefault(permission =>
                permission.Action.Equals("query", StringComparison.OrdinalIgnoreCase)) ??
                page.Permissions.FirstOrDefault();

            await UpsertPageAsync(pageId, parentId, page, accessPermission?.Code, cancellationToken);
            EnsureAdminRoleMenu(pageId, adminMenuIds);

            for (var index = 0; index < page.Permissions.Count; index++)
            {
                var permission = page.Permissions[index];
                var permissionId = PageIdentity.ForPermission(permission);
                await UpsertPermissionAsync(
                    permissionId,
                    pageId,
                    permission,
                    index + 1,
                    cancellationToken);
                EnsureAdminRoleMenu(permissionId, adminMenuIds);
            }
        }
    }

    private async Task UpsertPageAsync(
        Guid id,
        Guid? parentId,
        PageDefinition definition,
        string? permissionCode,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Menus.SingleOrDefaultAsync(
            menu => menu.Id == id,
            cancellationToken);
        if (existing is null)
        {
            dbContext.Menus.Add(new Menu
            {
                Id = id,
                ParentId = parentId,
                Name = definition.Key,
                Path = definition.Path,
                Component = definition.Component,
                Redirect = definition.Redirect,
                Title = definition.Title.ZhCn,
                Icon = definition.Icon,
                Order = definition.Order,
                PermissionCode = permissionCode,
                IsEnabled = true,
                IsVisible = definition.IsVisible
            });
            return;
        }

        existing.ParentId = parentId;
        existing.Name = definition.Key;
        existing.Path = definition.Path;
        existing.Component = definition.Component;
        existing.Redirect = definition.Redirect;
        existing.Title = definition.Title.ZhCn;
        existing.Icon = definition.Icon;
        existing.Order = definition.Order;
        existing.PermissionCode = permissionCode;
        existing.IsEnabled = true;
        existing.IsVisible = definition.IsVisible;
    }

    private async Task UpsertPermissionAsync(
        Guid id,
        Guid parentId,
        PermissionDefinition definition,
        int order,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Menus.SingleOrDefaultAsync(
            menu => menu.Id == id,
            cancellationToken);
        if (existing is null)
        {
            dbContext.Menus.Add(new Menu
            {
                Id = id,
                ParentId = parentId,
                Name = $"Permission_{definition.Code.Replace(':', '_')}",
                Path = definition.Code,
                Title = definition.Title.ZhCn,
                Order = order,
                PermissionCode = definition.Code,
                IsEnabled = true,
                IsVisible = false
            });
            return;
        }

        existing.ParentId = parentId;
        existing.Name = $"Permission_{definition.Code.Replace(':', '_')}";
        existing.Path = definition.Code;
        existing.Title = definition.Title.ZhCn;
        existing.PermissionCode = definition.Code;
        existing.IsEnabled = true;
        existing.IsVisible = false;
    }

    private void EnsureAdminRoleMenu(Guid menuId, ISet<Guid> adminMenuIds)
    {
        if (!adminMenuIds.Add(menuId))
        {
            return;
        }

        dbContext.RoleMenus.Add(new RoleMenu
        {
            RoleId = MiniAdminSeedIds.AdminRoleId,
            MenuId = menuId
        });
    }
}
