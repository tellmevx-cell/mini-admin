using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using MiniAdmin.Application.Contracts.Caching;
using MiniAdmin.Application.Contracts.DataScopes;
using MiniAdmin.Application.Contracts.PermissionDiagnostics;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Infrastructure.Caching;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfPermissionDiagnosticsRepository(
    MiniAdminDbContext dbContext,
    IDataScopeProvider dataScopeProvider,
    IUserAuthorizationCache userAuthorizationCache,
    IOptions<CacheOptions> cacheOptions) : IPermissionDiagnosticsRepository
{
    private readonly CacheOptions options = cacheOptions.Value;

    public async Task<PermissionDiagnosticsDto?> GetByUserNameAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserName = userName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUserName))
        {
            return null;
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .Include(x => x.Department)
            .Include(x => x.Position)
            .Include(x => x.Tenant)
            .ThenInclude(x => x!.Package)
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.UserName == normalizedUserName, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var allRoleIds = user.UserRoles
            .Select(x => x.RoleId)
            .Distinct()
            .ToList();
        var activeRoleIds = user.UserRoles
            .Where(x => x.Role.IsEnabled)
            .Select(x => x.RoleId)
            .Distinct()
            .ToList();
        RoleMenuEntry[] roleMenuEntries = allRoleIds.Count == 0
            ? []
            : await dbContext.RoleMenus
                .AsNoTracking()
                .Where(x => allRoleIds.Contains(x.RoleId) && x.Menu.IsEnabled)
                .Select(x => new RoleMenuEntry(x.RoleId, x.MenuId, x.Menu.IsVisible))
                .ToArrayAsync(cancellationToken);
        var roleMenuLookup = roleMenuEntries.ToLookup(x => x.RoleId);
        var roleCustomDepartmentIds = user.UserRoles
            .Select(x => x.Role)
            .ToDictionary(
                x => x.Id,
                x => ParseCustomDepartmentIds(x.CustomDepartmentIds));
        var dataScope = await dataScopeProvider.GetAsync(user.UserName, cancellationToken);
        var departmentNameLookup = await BuildDepartmentNameLookupAsync(
            dataScope.DepartmentIds,
            roleCustomDepartmentIds.Values.SelectMany(x => x),
            cancellationToken);
        var roles = user.UserRoles
            .Select(x => x.Role)
            .OrderBy(x => x.Code)
            .Select(x =>
            {
                var roleMenus = roleMenuLookup[x.Id].DistinctBy(menu => menu.MenuId).ToArray();
                var customDepartmentIds = roleCustomDepartmentIds[x.Id];

                return new PermissionDiagnosticsRoleDto(
                    x.Id.ToString(),
                    x.Code,
                    x.Name,
                    x.DataScope,
                    customDepartmentIds,
                    customDepartmentIds
                        .Select(id => departmentNameLookup.GetValueOrDefault(id))
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Cast<string>()
                        .Distinct()
                        .Order()
                        .ToArray(),
                    x.IsEnabled,
                    roleMenus.Length,
                    roleMenus.Count(menu => menu.IsVisible),
                    roleMenus.Count(menu => !menu.IsVisible));
            })
            .ToArray();
        var tenantPackageMenuIds = GetTenantPackageMenuIds(user);
        var menus = await GetAuthorizedMenusAsync(
            activeRoleIds,
            roleMenuEntries,
            tenantPackageMenuIds,
            cancellationToken);
        var permissionCodes = menus
            .Where(x => !string.IsNullOrWhiteSpace(x.PermissionCode))
            .Select(x => x.PermissionCode!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var tenant = BuildTenantDto(user, tenantPackageMenuIds);
        var effective = new PermissionDiagnosticsEffectiveDto(
            roleMenuEntries
                .Where(x => activeRoleIds.Contains(x.RoleId))
                .Select(x => x.MenuId)
                .Distinct()
                .Count(),
            tenantPackageMenuIds?.Count ?? 0,
            menus.Count,
            menus.Count(x => x.IsVisible),
            menus.Count(x => !x.IsVisible),
            permissionCodes.Length);
        var warnings = BuildWarnings(user, activeRoleIds, effective, tenant).ToArray();

        return new PermissionDiagnosticsDto(
            new PermissionDiagnosticsUserDto(
                user.Id.ToString(),
                user.UserName,
                user.RealName,
                user.Department?.Name,
                user.Position?.Name,
                user.IsEnabled),
            tenant,
            roles,
            permissionCodes,
            menus
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Name)
                .Select(x => new PermissionDiagnosticsMenuDto(
                    x.Id.ToString(),
                    x.Title,
                    x.Path,
                    x.PermissionCode,
                    x.IsVisible))
                .ToArray(),
            effective,
            warnings,
            ToDataScopeDto(dataScope, departmentNameLookup),
            BuildCacheDto(user.Id, user.UserName));
    }

    public async Task<bool> RefreshUserCacheAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserName = userName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUserName))
        {
            return false;
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserName == normalizedUserName, cancellationToken);
        if (user is null)
        {
            return false;
        }

        await userAuthorizationCache.RemoveUserAsync(user.Id, user.UserName, cancellationToken);
        return true;
    }

    private async Task<IReadOnlyList<Menu>> GetAuthorizedMenusAsync(
        IReadOnlyList<Guid> roleIds,
        IReadOnlyList<RoleMenuEntry> roleMenuEntries,
        HashSet<Guid>? tenantPackageMenuIds,
        CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0)
        {
            return Array.Empty<Menu>();
        }

        var menuIds = roleMenuEntries
            .Where(x => roleIds.Contains(x.RoleId))
            .Select(x => x.MenuId)
            .Distinct()
            .ToHashSet();
        if (tenantPackageMenuIds is not null)
        {
            menuIds.IntersectWith(tenantPackageMenuIds);
        }

        await AddAncestorMenuIdsAsync(menuIds, cancellationToken);

        return await dbContext.Menus
            .AsNoTracking()
            .Where(x => menuIds.Contains(x.Id) && x.IsEnabled)
            .ToArrayAsync(cancellationToken);
    }

    private static HashSet<Guid>? GetTenantPackageMenuIds(User user)
    {
        if (!user.TenantId.HasValue || user.Tenant?.Package is null)
        {
            return null;
        }

        return EfTenantPackageRepository.ParseMenuIds(user.Tenant.Package.MenuIds);
    }

    private static PermissionDiagnosticsTenantDto BuildTenantDto(User user, HashSet<Guid>? packageMenuIds)
    {
        if (!user.TenantId.HasValue)
        {
            return new PermissionDiagnosticsTenantDto(
                false,
                null,
                null,
                null,
                null,
                null,
                0,
                false);
        }

        return new PermissionDiagnosticsTenantDto(
            true,
            user.TenantId.Value.ToString(),
            user.Tenant?.Code,
            user.Tenant?.Name,
            user.Tenant?.PackageId?.ToString(),
            user.Tenant?.Package?.Name,
            packageMenuIds?.Count ?? 0,
            packageMenuIds is not null);
    }

    private static IEnumerable<PermissionDiagnosticsWarningDto> BuildWarnings(
        User user,
        IReadOnlyCollection<Guid> activeRoleIds,
        PermissionDiagnosticsEffectiveDto effective,
        PermissionDiagnosticsTenantDto tenant)
    {
        if (!user.IsEnabled)
        {
            yield return new PermissionDiagnosticsWarningDto(
                "UserDisabled",
                "error",
                "用户已禁用，登录和权限生效会被拦截。",
                "启用用户后重新登录，并刷新权限缓存。");
        }

        if (activeRoleIds.Count == 0)
        {
            yield return new PermissionDiagnosticsWarningDto(
                "NoActiveRoles",
                "error",
                "用户没有可用角色，无法获得菜单和按钮权限。",
                "给用户分配至少一个启用状态的角色。");
        }

        if (effective.RoleMenuCount == 0)
        {
            yield return new PermissionDiagnosticsWarningDto(
                "NoRoleMenus",
                "warning",
                "用户的启用角色没有分配任何菜单。",
                "到角色管理中为对应角色分配菜单和按钮权限。");
        }

        if (tenant.IsTenant && tenant.IsPackageLimited && effective.PackageMenuCount == 0)
        {
            yield return new PermissionDiagnosticsWarningDto(
                "NoTenantPackageMenus",
                "warning",
                "租户套餐没有分配菜单，租户用户无法获得菜单。",
                "到平台管理的租户套餐中为套餐分配菜单。");
        }

        if (tenant.IsTenant
            && tenant.IsPackageLimited
            && effective.RoleMenuCount > 0
            && effective.PackageMenuCount > 0
            && effective.FinalMenuCount == 0)
        {
            yield return new PermissionDiagnosticsWarningDto(
                "PackageFilteredAllRoleMenus",
                "warning",
                "角色菜单与租户套餐菜单没有交集，最终菜单为空。",
                "检查角色菜单是否在该租户套餐允许的菜单范围内。");
        }
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
            .ToArrayAsync(cancellationToken);
        var missingParentIds = parentIds
            .Where(parentId => !menuIds.Contains(parentId))
            .ToHashSet();

        while (missingParentIds.Count > 0)
        {
            var parentMenus = await dbContext.Menus
                .AsNoTracking()
                .Where(x => missingParentIds.Contains(x.Id) && x.IsEnabled)
                .ToArrayAsync(cancellationToken);

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

    private async Task<Dictionary<string, string>> BuildDepartmentNameLookupAsync(
        IEnumerable<Guid> dataScopeDepartmentIds,
        IEnumerable<string> roleCustomDepartmentIds,
        CancellationToken cancellationToken)
    {
        var departmentIds = dataScopeDepartmentIds
            .Select(id => id.ToString())
            .Concat(roleCustomDepartmentIds)
            .Distinct()
            .Select(id => Guid.TryParse(id, out var value) ? value : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        if (departmentIds.Count == 0)
        {
            return [];
        }

        return await dbContext.Departments
            .AsNoTracking()
            .Where(x => departmentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id.ToString(), x => x.Name, cancellationToken);
    }

    private PermissionDiagnosticsDataScopeDto ToDataScopeDto(
        DataScopeContext dataScope,
        IReadOnlyDictionary<string, string> departmentNameLookup)
    {
        var departmentIds = dataScope.DepartmentIds.Select(x => x.ToString()).Order().ToArray();
        return new PermissionDiagnosticsDataScopeDto(
            dataScope.Level.ToString(),
            GetDataScopeDescription(dataScope.Level),
            dataScope.DepartmentId?.ToString(),
            departmentIds,
            departmentIds
                .Select(id => departmentNameLookup.GetValueOrDefault(id))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .Distinct()
                .Order()
                .ToArray());
    }

    private static string GetDataScopeDescription(DataScopeLevel level)
    {
        return level switch
        {
            DataScopeLevel.All => "全部数据",
            DataScopeLevel.DepartmentAndChildren => "本部门及子部门",
            DataScopeLevel.Department => "本部门",
            DataScopeLevel.CustomDepartments => "自定义部门",
            DataScopeLevel.Mixed => "组合范围",
            DataScopeLevel.Self => "仅本人",
            _ => "无数据权限"
        };
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

    private PermissionDiagnosticsCacheDto BuildCacheDto(Guid userId, string userName)
    {
        var prefix = NormalizePrefix();
        var normalizedUserName = userName.Trim().ToLowerInvariant();

        return new PermissionDiagnosticsCacheDto(
            $"{prefix}auth:security-stamp:{userId:N}",
            $"{prefix}auth:permissions:{normalizedUserName}",
            $"{prefix}auth:menus:{normalizedUserName}");
    }

    private string NormalizePrefix()
    {
        return string.IsNullOrWhiteSpace(options.KeyPrefix)
            ? "mini-admin:"
            : options.KeyPrefix.Trim();
    }

    private sealed record RoleMenuEntry(Guid RoleId, Guid MenuId, bool IsVisible);
}
