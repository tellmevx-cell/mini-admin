using Microsoft.EntityFrameworkCore;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public interface IGeneratedCrudSeedDefinition
{
    Task SeedAsync(MiniAdminDbContext dbContext, CancellationToken cancellationToken = default);
}

public abstract class GeneratedCrudSeedDefinitionBase : IGeneratedCrudSeedDefinition
{
    public abstract Task SeedAsync(MiniAdminDbContext dbContext, CancellationToken cancellationToken = default);

    protected static async Task EnsureMenuAsync(
        MiniAdminDbContext dbContext,
        Menu menu,
        CancellationToken cancellationToken)
    {
        var existingMenu = await dbContext.Menus.SingleOrDefaultAsync(x => x.Id == menu.Id, cancellationToken);
        if (existingMenu is not null)
        {
            existingMenu.ParentId = menu.ParentId;
            existingMenu.Name = menu.Name;
            existingMenu.Path = menu.Path;
            existingMenu.Component = menu.Component;
            existingMenu.Redirect = menu.Redirect;
            existingMenu.Title = menu.Title;
            existingMenu.Icon = menu.Icon;
            existingMenu.Order = menu.Order;
            existingMenu.PermissionCode = menu.PermissionCode;
            existingMenu.IsEnabled = menu.IsEnabled;
            existingMenu.IsVisible = menu.IsVisible;
            return;
        }

        dbContext.Menus.Add(menu);
    }

    protected static async Task EnsureAdminRoleMenuAsync(
        MiniAdminDbContext dbContext,
        Guid menuId,
        CancellationToken cancellationToken)
    {
        if (await dbContext.RoleMenus.AnyAsync(
            x => x.RoleId == MiniAdminSeedIds.AdminRoleId && x.MenuId == menuId,
            cancellationToken))
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
