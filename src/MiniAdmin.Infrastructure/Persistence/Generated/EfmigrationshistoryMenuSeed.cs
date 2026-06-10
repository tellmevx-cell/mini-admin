using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence.Generated;

public sealed class EfmigrationshistoryMenuSeed : GeneratedCrudSeedDefinitionBase
{
    public override async Task SeedAsync(MiniAdminDbContext dbContext, CancellationToken cancellationToken = default)
    {
        Guid? parentMenuId = null;
        var menuId = Guid.Parse("9e748e07-787d-27d5-a030-7eca96f76929");
        var queryPermissionId = Guid.Parse("6aa3ac33-ec7a-97f9-1829-766fbe946b37");
        var createPermissionId = Guid.Parse("1ebb7b2b-9d5a-e9be-0f70-a813345e42e5");
        var updatePermissionId = Guid.Parse("10883f9e-01fe-8b37-eb62-d1a8542e1d8b");
        var deletePermissionId = Guid.Parse("354da1bc-01e2-3236-5c55-7e01a715f27b");

        var importPermissionId = Guid.Parse("9c06447d-8fa0-a444-1afa-51bf918ba282");
        var exportPermissionId = Guid.Parse("031d81bd-fa46-c913-ce86-29278b643c12");

        var submitWorkflowPermissionId = Guid.Parse("8c1132df-82b9-b650-e31a-0b963bcafd84");
        var withdrawWorkflowPermissionId = Guid.Parse("5bd83921-83ec-fb3c-6808-11c716930527");

        await EnsureMenuAsync(dbContext, new Menu
        {
            Id = menuId,
            ParentId = parentMenuId,
            Name = "Efmigrationshistory",
            Path = "/business/efmigrationshistory",
            Component = "/business/efmigrationshistory/index",
            Title = "__efmigrationshistory",
            Icon = "lucide:table-2",
            Order = 100,
            PermissionCode = "business:efmigrationshistory:query",
            IsEnabled = true,
            IsVisible = true
        }, cancellationToken);

        await EnsureMenuAsync(dbContext, new Menu
        {
            Id = queryPermissionId,
            ParentId = menuId,
            Name = "EfmigrationshistoryQueryPermission",
            Path = "business:efmigrationshistory:query",
            Title = "business:efmigrationshistory:query",
            Order = 1,
            PermissionCode = "business:efmigrationshistory:query",
            IsEnabled = true,
            IsVisible = false
        }, cancellationToken);

        await EnsureMenuAsync(dbContext, new Menu
        {
            Id = createPermissionId,
            ParentId = menuId,
            Name = "EfmigrationshistoryCreatePermission",
            Path = "business:efmigrationshistory:create",
            Title = "business:efmigrationshistory:create",
            Order = 2,
            PermissionCode = "business:efmigrationshistory:create",
            IsEnabled = true,
            IsVisible = false
        }, cancellationToken);

        await EnsureMenuAsync(dbContext, new Menu
        {
            Id = updatePermissionId,
            ParentId = menuId,
            Name = "EfmigrationshistoryUpdatePermission",
            Path = "business:efmigrationshistory:update",
            Title = "business:efmigrationshistory:update",
            Order = 3,
            PermissionCode = "business:efmigrationshistory:update",
            IsEnabled = true,
            IsVisible = false
        }, cancellationToken);

        await EnsureMenuAsync(dbContext, new Menu
        {
            Id = deletePermissionId,
            ParentId = menuId,
            Name = "EfmigrationshistoryDeletePermission",
            Path = "business:efmigrationshistory:delete",
            Title = "business:efmigrationshistory:delete",
            Order = 4,
            PermissionCode = "business:efmigrationshistory:delete",
            IsEnabled = true,
            IsVisible = false
        }, cancellationToken);

        await EnsureMenuAsync(dbContext, new Menu
        {
            Id = importPermissionId,
            ParentId = menuId,
            Name = "EfmigrationshistoryImportPermission",
            Path = "business:efmigrationshistory:import",
            Title = "business:efmigrationshistory:import",
            Order = 5,
            PermissionCode = "business:efmigrationshistory:import",
            IsEnabled = true,
            IsVisible = false
        }, cancellationToken);

        await EnsureMenuAsync(dbContext, new Menu
        {
            Id = exportPermissionId,
            ParentId = menuId,
            Name = "EfmigrationshistoryExportPermission",
            Path = "business:efmigrationshistory:export",
            Title = "business:efmigrationshistory:export",
            Order = 6,
            PermissionCode = "business:efmigrationshistory:export",
            IsEnabled = true,
            IsVisible = false
        }, cancellationToken);

        await EnsureMenuAsync(dbContext, new Menu
        {
            Id = submitWorkflowPermissionId,
            ParentId = menuId,
            Name = "EfmigrationshistorySubmitWorkflowPermission",
            Path = "business:efmigrationshistory:submit-workflow",
            Title = "business:efmigrationshistory:submit-workflow",
            Order = 7,
            PermissionCode = "business:efmigrationshistory:submit-workflow",
            IsEnabled = true,
            IsVisible = false
        }, cancellationToken);

        await EnsureMenuAsync(dbContext, new Menu
        {
            Id = withdrawWorkflowPermissionId,
            ParentId = menuId,
            Name = "EfmigrationshistoryWithdrawWorkflowPermission",
            Path = "business:efmigrationshistory:withdraw-workflow",
            Title = "business:efmigrationshistory:withdraw-workflow",
            Order = 8,
            PermissionCode = "business:efmigrationshistory:withdraw-workflow",
            IsEnabled = true,
            IsVisible = false
        }, cancellationToken);

        await EnsureAdminRoleMenuAsync(dbContext, menuId, cancellationToken);
        await EnsureAdminRoleMenuAsync(dbContext, queryPermissionId, cancellationToken);
        await EnsureAdminRoleMenuAsync(dbContext, createPermissionId, cancellationToken);
        await EnsureAdminRoleMenuAsync(dbContext, updatePermissionId, cancellationToken);
        await EnsureAdminRoleMenuAsync(dbContext, deletePermissionId, cancellationToken);
         await EnsureAdminRoleMenuAsync(dbContext, importPermissionId, cancellationToken);
         await EnsureAdminRoleMenuAsync(dbContext, exportPermissionId, cancellationToken);
         await EnsureAdminRoleMenuAsync(dbContext, submitWorkflowPermissionId, cancellationToken);
         await EnsureAdminRoleMenuAsync(dbContext, withdrawWorkflowPermissionId, cancellationToken);
    }
}