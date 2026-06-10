using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence.Generated;

public sealed class SampleOrderMenuSeed : GeneratedCrudSeedDefinitionBase
{
    public override async Task SeedAsync(MiniAdminDbContext dbContext, CancellationToken cancellationToken = default)
    {
        Guid? parentMenuId = null;
        var menuId = Guid.Parse("ea6ba4c5-2c6a-aaff-2061-c477e14d4c4f");
        var queryPermissionId = Guid.Parse("e66cc1d7-d57d-336c-beda-385f10d8d22c");
        var createPermissionId = Guid.Parse("51e067a7-5a49-e77e-89be-d49658ce87c7");
        var updatePermissionId = Guid.Parse("db3fdd2b-be96-e533-7a35-85522b1a6cbb");
        var deletePermissionId = Guid.Parse("26601485-c5c3-1fc6-6606-0fa51f39b5b1");
        var submitWorkflowPermissionId = Guid.Parse("b6bc69a7-64f2-4ec0-8f7c-7c57c3168e53");
        var withdrawWorkflowPermissionId = Guid.Parse("0ef55a19-3b0e-49ad-8e38-68f6723b3689");

        await EnsureMenuAsync(dbContext, new Menu
        {
            Id = menuId,
            ParentId = parentMenuId,
            Name = "SampleOrder",
            Path = "/business/sample-order",
            Component = "/business/sample-order/index",
            Title = "示例订单",
            Icon = "lucide:table-2",
            Order = 100,
            PermissionCode = "business:sample-order:query",
            IsEnabled = true,
            IsVisible = true
        }, cancellationToken);

        await EnsureMenuAsync(dbContext, new Menu
        {
            Id = queryPermissionId,
            ParentId = menuId,
            Name = "SampleOrderQueryPermission",
            Path = "business:sample-order:query",
            Title = "business:sample-order:query",
            Order = 1,
            PermissionCode = "business:sample-order:query",
            IsEnabled = true,
            IsVisible = false
        }, cancellationToken);

        await EnsureMenuAsync(dbContext, new Menu
        {
            Id = createPermissionId,
            ParentId = menuId,
            Name = "SampleOrderCreatePermission",
            Path = "business:sample-order:create",
            Title = "business:sample-order:create",
            Order = 2,
            PermissionCode = "business:sample-order:create",
            IsEnabled = true,
            IsVisible = false
        }, cancellationToken);

        await EnsureMenuAsync(dbContext, new Menu
        {
            Id = updatePermissionId,
            ParentId = menuId,
            Name = "SampleOrderUpdatePermission",
            Path = "business:sample-order:update",
            Title = "business:sample-order:update",
            Order = 3,
            PermissionCode = "business:sample-order:update",
            IsEnabled = true,
            IsVisible = false
        }, cancellationToken);

        await EnsureMenuAsync(dbContext, new Menu
        {
            Id = deletePermissionId,
            ParentId = menuId,
            Name = "SampleOrderDeletePermission",
            Path = "business:sample-order:delete",
            Title = "business:sample-order:delete",
            Order = 4,
            PermissionCode = "business:sample-order:delete",
            IsEnabled = true,
            IsVisible = false
        }, cancellationToken);

        await EnsureMenuAsync(dbContext, new Menu
        {
            Id = submitWorkflowPermissionId,
            ParentId = menuId,
            Name = "SampleOrderSubmitWorkflowPermission",
            Path = "business:sample-order:submit-workflow",
            Title = "business:sample-order:submit-workflow",
            Order = 5,
            PermissionCode = "business:sample-order:submit-workflow",
            IsEnabled = true,
            IsVisible = false
        }, cancellationToken);

        await EnsureMenuAsync(dbContext, new Menu
        {
            Id = withdrawWorkflowPermissionId,
            ParentId = menuId,
            Name = "SampleOrderWithdrawWorkflowPermission",
            Path = "business:sample-order:withdraw-workflow",
            Title = "business:sample-order:withdraw-workflow",
            Order = 6,
            PermissionCode = "business:sample-order:withdraw-workflow",
            IsEnabled = true,
            IsVisible = false
        }, cancellationToken);

        await EnsureAdminRoleMenuAsync(dbContext, menuId, cancellationToken);
        await EnsureAdminRoleMenuAsync(dbContext, queryPermissionId, cancellationToken);
        await EnsureAdminRoleMenuAsync(dbContext, createPermissionId, cancellationToken);
        await EnsureAdminRoleMenuAsync(dbContext, updatePermissionId, cancellationToken);
        await EnsureAdminRoleMenuAsync(dbContext, deletePermissionId, cancellationToken);
        await EnsureAdminRoleMenuAsync(dbContext, submitWorkflowPermissionId, cancellationToken);
        await EnsureAdminRoleMenuAsync(dbContext, withdrawWorkflowPermissionId, cancellationToken);
    }
}
