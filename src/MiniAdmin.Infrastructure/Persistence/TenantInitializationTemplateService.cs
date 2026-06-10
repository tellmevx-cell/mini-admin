using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Tenants;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class TenantInitializationTemplateService(MiniAdminDbContext dbContext)
{
    public const string StandardTemplateCode = "standard";

    public IReadOnlyList<TenantInitializationTemplateDto> GetTemplates()
    {
        return
        [
            new TenantInitializationTemplateDto(
                StandardTemplateCode,
                "标准企业模板",
                "初始化总部、研发部、市场部、常用岗位和普通员工角色。",
                true)
        ];
    }

    public async Task InitializeAsync(
        Tenant tenant,
        User adminUser,
        string? requestedTemplateCode,
        CancellationToken cancellationToken)
    {
        var templateCode = NormalizeTemplateCode(requestedTemplateCode);
        if (templateCode != StandardTemplateCode)
        {
            throw new InvalidOperationException("初始化模板不存在");
        }

        tenant.InitializationTemplateCode = templateCode;
        tenant.InitializationStatus = "Pending";
        tenant.InitializedAt = null;
        tenant.InitializationError = null;

        try
        {
            var headquartersId = await EnsureDepartmentAsync(
                tenant.Id,
                null,
                "HQ",
                "总部",
                1,
                cancellationToken);
            await EnsureDepartmentAsync(
                tenant.Id,
                headquartersId,
                "RD",
                "研发部",
                10,
                cancellationToken);
            await EnsureDepartmentAsync(
                tenant.Id,
                headquartersId,
                "MKT",
                "市场部",
                20,
                cancellationToken);

            var deptLeadPositionId = await EnsurePositionAsync(
                tenant.Id,
                "dept-lead",
                "部门负责人",
                1,
                cancellationToken);
            await EnsurePositionAsync(
                tenant.Id,
                "developer",
                "开发工程师",
                10,
                cancellationToken);
            await EnsurePositionAsync(
                tenant.Id,
                "sales-manager",
                "销售经理",
                20,
                cancellationToken);

            adminUser.DepartmentId ??= headquartersId;
            adminUser.PositionId ??= deptLeadPositionId;

            var employeeRoleId = await EnsureEmployeeRoleAsync(tenant.Id, cancellationToken);
            foreach (var menuId in GetEmployeeMenuIds())
            {
                await EnsureRoleMenuAsync(employeeRoleId, menuId, cancellationToken);
            }

            tenant.InitializationStatus = "Success";
            tenant.InitializedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            tenant.InitializationStatus = "Failed";
            tenant.InitializationError = ex.Message.Length > 512
                ? ex.Message[..512]
                : ex.Message;
            throw;
        }
    }

    private async Task<Guid> EnsureDepartmentAsync(
        Guid tenantId,
        Guid? parentId,
        string code,
        string name,
        int order,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Departments.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.Code == code,
            cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var departmentId = Guid.NewGuid();
        dbContext.Departments.Add(new Department
        {
            Id = departmentId,
            TenantId = tenantId,
            ParentId = parentId,
            Code = code,
            Name = name,
            Order = order,
            IsEnabled = true
        });

        return departmentId;
    }

    private async Task<Guid> EnsurePositionAsync(
        Guid tenantId,
        string code,
        string name,
        int order,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Positions.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.Code == code,
            cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var positionId = Guid.NewGuid();
        dbContext.Positions.Add(new Position
        {
            Id = positionId,
            TenantId = tenantId,
            Code = code,
            Name = name,
            Order = order,
            IsEnabled = true,
            Remark = "租户标准模板初始化"
        });

        return positionId;
    }

    private async Task<Guid> EnsureEmployeeRoleAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.Code == "employee",
            cancellationToken);
        if (role is not null)
        {
            return role.Id;
        }

        var roleId = Guid.NewGuid();
        dbContext.Roles.Add(new Role
        {
            Id = roleId,
            TenantId = tenantId,
            Code = "employee",
            Name = "普通员工",
            DataScope = "self",
            IsEnabled = true
        });

        return roleId;
    }

    private async Task EnsureRoleMenuAsync(
        Guid roleId,
        Guid menuId,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.RoleMenus.AnyAsync(
            x => x.RoleId == roleId && x.MenuId == menuId,
            cancellationToken);
        if (exists)
        {
            return;
        }

        dbContext.RoleMenus.Add(new RoleMenu
        {
            RoleId = roleId,
            MenuId = menuId
        });
    }

    private static string NormalizeTemplateCode(string? templateCode)
    {
        return string.IsNullOrWhiteSpace(templateCode)
            ? StandardTemplateCode
            : templateCode.Trim().ToLowerInvariant();
    }

    private static IReadOnlyList<Guid> GetEmployeeMenuIds()
    {
        return
        [
            MiniAdminSeedIds.DashboardMenuId,
            MiniAdminSeedIds.AnalyticsMenuId,
            MiniAdminSeedIds.WorkspaceMenuId,
            MiniAdminSeedIds.SystemMenuId,
            MiniAdminSeedIds.UserManagementMenuId,
            MiniAdminSeedIds.UserQueryPermissionId,
            MiniAdminSeedIds.RoleManagementMenuId,
            MiniAdminSeedIds.RoleQueryPermissionId,
            MiniAdminSeedIds.DepartmentManagementMenuId,
            MiniAdminSeedIds.DepartmentQueryPermissionId,
            MiniAdminSeedIds.PositionManagementMenuId,
            MiniAdminSeedIds.PositionQueryPermissionId
        ];
    }
}
