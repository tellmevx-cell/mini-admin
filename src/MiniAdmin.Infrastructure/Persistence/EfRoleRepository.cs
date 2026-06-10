using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using MiniAdmin.Application.Contracts.Caching;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.Roles;
using MiniAdmin.Application.Contracts.Security;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfRoleRepository(
    MiniAdminDbContext dbContext,
    IUserAuthorizationCache userAuthorizationCache,
    ISecurityEventRepository securityEventRepository,
    ICurrentTenant currentTenant) : IRoleRepository
{
    public async Task<PageResult<RoleListItemDto>> GetListAsync(
        RoleListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var rolesQuery = ApplyTenantScope(dbContext.Roles.AsNoTracking());

        if (!string.IsNullOrWhiteSpace(query.Code))
        {
            rolesQuery = rolesQuery.Where(x => x.Code.Contains(query.Code));
        }

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            rolesQuery = rolesQuery.Where(x => x.Name.Contains(query.Name));
        }

        var total = await rolesQuery.CountAsync(cancellationToken);
        var items = await rolesQuery
            .OrderBy(x => x.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToDto(x))
            .ToArrayAsync(cancellationToken);

        return new PageResult<RoleListItemDto>(items, total);
    }

    public async Task<RoleListItemDto> CreateAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = currentTenant.TenantId,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            DataScope = NormalizeDataScope(request.DataScope),
            CustomDepartmentIds = await NormalizeCustomDepartmentIdsAsync(
                request.DataScope,
                request.CustomDepartmentIds,
                cancellationToken),
            IsEnabled = request.IsEnabled
        };

        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(role);
    }

    public async Task<RoleListItemDto?> UpdateAsync(
        Guid id,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var role = await ApplyTenantScope(dbContext.Roles)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (role is null)
        {
            return null;
        }

        role.Name = request.Name.Trim();
        role.DataScope = NormalizeDataScope(request.DataScope);
        role.CustomDepartmentIds = await NormalizeCustomDepartmentIdsAsync(
            request.DataScope,
            request.CustomDepartmentIds,
            cancellationToken);
        role.IsEnabled = request.IsEnabled;
        var affectedUsers = await RefreshUsersByRoleAsync(role.Id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RemoveUserCachesAsync(affectedUsers, cancellationToken);

        return ToDto(role);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await ApplyTenantScope(dbContext.Roles)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (role is null)
        {
            return false;
        }

        if (role.Code == "admin")
        {
            return false;
        }

        var isAssignedToUsers = await dbContext.UserRoles
            .AnyAsync(x => x.RoleId == id, cancellationToken);
        if (isAssignedToUsers)
        {
            return false;
        }

        dbContext.Roles.Remove(role);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<string>> GetMenuIdsAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var menuIds = await dbContext.RoleMenus
            .AsNoTracking()
            .Where(x => x.RoleId == roleId &&
                        (currentTenant.IsPlatform
                            ? x.Role.TenantId == null
                            : x.Role.TenantId == currentTenant.TenantId))
            .Select(x => x.MenuId)
            .ToArrayAsync(cancellationToken);
        var packageMenuIds = await GetCurrentTenantPackageMenuIdsAsync(cancellationToken);
        if (packageMenuIds is not null)
        {
            menuIds = menuIds.Where(packageMenuIds.Contains).ToArray();
        }

        return menuIds
            .Select(x => x.ToString())
            .Order()
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> UpdateMenuIdsAsync(
        Guid roleId,
        IReadOnlyList<Guid> menuIds,
        CancellationToken cancellationToken = default)
    {
        var roleExists = await ApplyTenantScope(dbContext.Roles)
            .AnyAsync(x => x.Id == roleId, cancellationToken);
        if (!roleExists)
        {
            return [];
        }

        var existingRoleMenus = await dbContext.RoleMenus
            .Where(x => x.RoleId == roleId)
            .ToListAsync(cancellationToken);
        dbContext.RoleMenus.RemoveRange(existingRoleMenus);

        var packageMenuIds = await GetCurrentTenantPackageMenuIdsAsync(cancellationToken);
        var requestedMenuIds = packageMenuIds is null
            ? menuIds.ToHashSet()
            : menuIds.Where(packageMenuIds.Contains).ToHashSet();

        var selectedMenus = await dbContext.Menus
            .AsNoTracking()
            .Where(x => requestedMenuIds.Contains(x.Id) && x.IsEnabled)
            .Distinct()
            .ToListAsync(cancellationToken);
        var selectedMenuIds = selectedMenus.Select(x => x.Id).ToHashSet();
        var parentIds = selectedMenus
            .Select(x => x.ParentId)
            .OfType<Guid>()
            .Where(parentId => !selectedMenuIds.Contains(parentId))
            .ToHashSet();
        var validMenuIds = selectedMenuIds;

        while (parentIds.Count > 0)
        {
            var parentMenus = await dbContext.Menus
                .AsNoTracking()
                .Where(x => parentIds.Contains(x.Id) && x.IsEnabled)
                .ToListAsync(cancellationToken);

            parentIds.Clear();
            foreach (var parentMenu in parentMenus)
            {
                if (!validMenuIds.Add(parentMenu.Id))
                {
                    continue;
                }

                if (parentMenu.ParentId is Guid grandParentId && !validMenuIds.Contains(grandParentId))
                {
                    parentIds.Add(grandParentId);
                }
            }
        }

        dbContext.RoleMenus.AddRange(validMenuIds.Select(menuId => new RoleMenu
        {
            RoleId = roleId,
            MenuId = menuId
        }));

        var affectedUsers = await RefreshUsersByRoleAsync(roleId, cancellationToken);
        await securityEventRepository.RecordEventAsync(
            new SaveSecurityEventRequest(
                "RolePermissionChanged",
                "Warning",
                "角色授权变更",
                $"角色 {roleId} 的菜单权限已变更，受影响用户旧 token 已失效.",
                RelatedEntityType: "Role",
                RelatedEntityId: roleId.ToString()),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RemoveUserCachesAsync(affectedUsers, cancellationToken);

        return validMenuIds
            .Select(x => x.ToString())
            .Order()
            .ToArray();
    }

    private static RoleListItemDto ToDto(Role role)
    {
        return new RoleListItemDto(
            role.Id.ToString(),
            role.Code,
            role.Name,
            role.DataScope,
            role.IsEnabled ? 1 : 0,
            ParseCustomDepartmentIds(role.CustomDepartmentIds));
    }

    private IQueryable<Role> ApplyTenantScope(IQueryable<Role> rolesQuery)
    {
        var tenantId = currentTenant.TenantId;
        return tenantId.HasValue
            ? rolesQuery.Where(x => x.TenantId == tenantId.Value)
            : rolesQuery.Where(x => x.TenantId == null);
    }

    private IQueryable<Department> ApplyDepartmentTenantScope(IQueryable<Department> departmentsQuery)
    {
        var tenantId = currentTenant.TenantId;
        return tenantId.HasValue
            ? departmentsQuery.Where(x => x.TenantId == tenantId.Value)
            : departmentsQuery.Where(x => x.TenantId == null);
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

    private static string NormalizeDataScope(string? dataScope)
    {
        return dataScope?.Trim() switch
        {
            "department-and-children" => "department-and-children",
            "department" => "department",
            "self" => "self",
            "custom" => "custom",
            _ => "all"
        };
    }

    private async Task<string?> NormalizeCustomDepartmentIdsAsync(
        string? dataScope,
        IReadOnlyList<Guid>? customDepartmentIds,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(NormalizeDataScope(dataScope), "custom", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var requestedIds = (customDepartmentIds ?? Array.Empty<Guid>())
            .Distinct()
            .ToArray();
        if (requestedIds.Length == 0)
        {
            throw new InvalidOperationException("自定义数据范围至少选择一个部门.");
        }

        var tenantDepartmentIds = await ApplyDepartmentTenantScope(dbContext.Departments.AsNoTracking())
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);
        var validIds = tenantDepartmentIds
            .Where(requestedIds.Contains)
            .ToArray();
        if (validIds.Length != requestedIds.Length)
        {
            throw new InvalidOperationException("存在无效部门或跨租户部门，无法保存自定义数据范围.");
        }

        return JsonSerializer.Serialize(validIds.Order().ToArray());
    }

    private static IReadOnlyList<string> ParseCustomDepartmentIds(string? customDepartmentIds)
    {
        if (string.IsNullOrWhiteSpace(customDepartmentIds))
        {
            return Array.Empty<string>();
        }

        try
        {
            return (JsonSerializer.Deserialize<Guid[]>(customDepartmentIds) ?? Array.Empty<Guid>())
                .Distinct()
                .Order()
                .Select(x => x.ToString())
                .ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private async Task<IReadOnlyList<UserCacheKey>> RefreshUsersByRoleAsync(
        Guid roleId,
        CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .Where(user => user.UserRoles.Any(userRole => userRole.RoleId == roleId))
            .ToArrayAsync(cancellationToken);

        foreach (var user in users)
        {
            user.SecurityStamp = CreateSecurityStamp();
        }

        return users
            .Select(user => new UserCacheKey(user.Id, user.UserName))
            .ToArray();
    }

    private async Task RemoveUserCachesAsync(
        IReadOnlyList<UserCacheKey> users,
        CancellationToken cancellationToken)
    {
        foreach (var user in users)
        {
            await userAuthorizationCache.RemoveUserAsync(user.Id, user.UserName, cancellationToken);
        }
    }

    private static string CreateSecurityStamp()
    {
        return Guid.NewGuid().ToString("N");
    }

    private sealed record UserCacheKey(Guid Id, string UserName);
}
