using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence.Generated;

public sealed class CustomerMenuSeed : GeneratedCrudSeedDefinitionBase
{
    public override async Task SeedAsync(MiniAdminDbContext dbContext, CancellationToken cancellationToken = default)
    {
        Guid? parentMenuId = null;
        var menuId = Guid.Parse("13dd0b09-ae84-eaf2-8de8-63b98e6ae903");
        var queryPermissionId = Guid.Parse("daaa964d-801a-0f36-690f-5226d7307dac");
        var createPermissionId = Guid.Parse("984fb2f8-98e6-328a-c63e-d2b0a19cced5");
        var updatePermissionId = Guid.Parse("c4ee2db6-3d3c-432c-77c4-e0d03839534d");
        var deletePermissionId = Guid.Parse("401f189a-7c24-b410-b061-81b74ace666b");

        await EnsureMenuAsync(dbContext, new Menu
        {
            Id = menuId,
            ParentId = parentMenuId,
            Name = "Customer",
            Path = "/business/customer",
            Component = "/business/customer/index",
            Title = "客户资料",
            Icon = "lucide:table-2",
            Order = 100,
            PermissionCode = "business:customer:query",
            IsEnabled = true,
            IsVisible = true
        }, cancellationToken);

        await EnsureMenuAsync(dbContext, new Menu
        {
            Id = queryPermissionId,
            ParentId = menuId,
            Name = "CustomerQueryPermission",
            Path = "business:customer:query",
            Title = "business:customer:query",
            Order = 1,
            PermissionCode = "business:customer:query",
            IsEnabled = true,
            IsVisible = false
        }, cancellationToken);

        await EnsureMenuAsync(dbContext, new Menu
        {
            Id = createPermissionId,
            ParentId = menuId,
            Name = "CustomerCreatePermission",
            Path = "business:customer:create",
            Title = "business:customer:create",
            Order = 2,
            PermissionCode = "business:customer:create",
            IsEnabled = true,
            IsVisible = false
        }, cancellationToken);

        await EnsureMenuAsync(dbContext, new Menu
        {
            Id = updatePermissionId,
            ParentId = menuId,
            Name = "CustomerUpdatePermission",
            Path = "business:customer:update",
            Title = "business:customer:update",
            Order = 3,
            PermissionCode = "business:customer:update",
            IsEnabled = true,
            IsVisible = false
        }, cancellationToken);

        await EnsureMenuAsync(dbContext, new Menu
        {
            Id = deletePermissionId,
            ParentId = menuId,
            Name = "CustomerDeletePermission",
            Path = "business:customer:delete",
            Title = "business:customer:delete",
            Order = 4,
            PermissionCode = "business:customer:delete",
            IsEnabled = true,
            IsVisible = false
        }, cancellationToken);

        await EnsureAdminRoleMenuAsync(dbContext, menuId, cancellationToken);
        await EnsureAdminRoleMenuAsync(dbContext, queryPermissionId, cancellationToken);
        await EnsureAdminRoleMenuAsync(dbContext, createPermissionId, cancellationToken);
        await EnsureAdminRoleMenuAsync(dbContext, updatePermissionId, cancellationToken);
        await EnsureAdminRoleMenuAsync(dbContext, deletePermissionId, cancellationToken);
    }
}
