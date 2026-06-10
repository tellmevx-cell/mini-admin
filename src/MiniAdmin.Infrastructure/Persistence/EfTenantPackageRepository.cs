using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Caching;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.TenantPackages;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfTenantPackageRepository(
    MiniAdminDbContext dbContext,
    IUserAuthorizationCache userAuthorizationCache) : ITenantPackageRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PageResult<TenantPackageDto>> GetListAsync(
        TenantPackageListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var packagesQuery = dbContext.TenantPackages.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            packagesQuery = packagesQuery.Where(x => x.Name.Contains(query.Name.Trim()));
        }

        if (query.IsEnabled.HasValue)
        {
            packagesQuery = packagesQuery.Where(x => x.IsEnabled == query.IsEnabled.Value);
        }

        var total = await packagesQuery.CountAsync(cancellationToken);
        var packages = await packagesQuery
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PageResult<TenantPackageDto>(
            packages.Select(ToDto).ToArray(),
            total);
    }

    public async Task<IReadOnlyList<TenantPackageOptionDto>> GetOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.TenantPackages
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new TenantPackageOptionDto(x.Id.ToString(), x.Name, x.IsEnabled))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<TenantPackageDto> CreateAsync(
        SaveTenantPackageRequest request,
        CancellationToken cancellationToken = default)
    {
        var package = new TenantPackage
        {
            Id = Guid.NewGuid(),
            Name = NormalizeRequired(request.Name, "套餐名称不能为空"),
            MaxUsers = Math.Max(request.MaxUsers, 0),
            MaxStorageMb = Math.Max(request.MaxStorageMb, 0),
            MenuIds = "[]",
            IsEnabled = request.IsEnabled,
            Remark = NormalizeOptional(request.Remark)
        };

        dbContext.TenantPackages.Add(package);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(package);
    }

    public async Task<TenantPackageDto?> UpdateAsync(
        Guid id,
        SaveTenantPackageRequest request,
        CancellationToken cancellationToken = default)
    {
        var package = await dbContext.TenantPackages.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (package is null)
        {
            return null;
        }

        package.Name = NormalizeRequired(request.Name, "套餐名称不能为空");
        package.MaxUsers = Math.Max(request.MaxUsers, 0);
        package.MaxStorageMb = Math.Max(request.MaxStorageMb, 0);
        package.IsEnabled = request.IsEnabled;
        package.Remark = NormalizeOptional(request.Remark);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(package);
    }

    public async Task<TenantPackageDto?> SetEnabledAsync(
        Guid id,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        var package = await dbContext.TenantPackages.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (package is null)
        {
            return null;
        }

        package.IsEnabled = isEnabled;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(package);
    }

    public async Task<IReadOnlyList<string>> GetMenuIdsAsync(
        Guid packageId,
        CancellationToken cancellationToken = default)
    {
        var menuIdsJson = await dbContext.TenantPackages
            .AsNoTracking()
            .Where(x => x.Id == packageId)
            .Select(x => x.MenuIds)
            .SingleOrDefaultAsync(cancellationToken);

        return ParseMenuIds(menuIdsJson)
            .Select(x => x.ToString())
            .Order()
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> UpdateMenuIdsAsync(
        Guid packageId,
        IReadOnlyList<Guid> menuIds,
        CancellationToken cancellationToken = default)
    {
        var package = await dbContext.TenantPackages.SingleOrDefaultAsync(x => x.Id == packageId, cancellationToken);
        if (package is null)
        {
            return [];
        }

        var validMenuIds = await BuildValidMenuIdsWithAncestorsAsync(menuIds, cancellationToken);
        package.MenuIds = JsonSerializer.Serialize(validMenuIds.Order().ToArray(), JsonOptions);

        var tenantIds = (await dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.PackageId == packageId)
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken))
            .ToList();
        var validMenuIdList = validMenuIds.ToList();
        var tenantRoleIds = (await dbContext.Roles
            .AsNoTracking()
            .Where(x => x.TenantId.HasValue && tenantIds.Contains(x.TenantId.Value))
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken))
            .ToList();
        var extraRoleMenus = await dbContext.RoleMenus
            .Where(x => tenantRoleIds.Contains(x.RoleId) && !validMenuIdList.Contains(x.MenuId))
            .ToArrayAsync(cancellationToken);
        dbContext.RoleMenus.RemoveRange(extraRoleMenus);

        var affectedUsers = await dbContext.Users
            .Where(x => x.TenantId.HasValue && tenantIds.Contains(x.TenantId.Value))
            .ToArrayAsync(cancellationToken);
        foreach (var user in affectedUsers)
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        foreach (var user in affectedUsers)
        {
            await userAuthorizationCache.RemoveUserAsync(user.Id, user.UserName, cancellationToken);
        }

        return validMenuIds
            .Select(x => x.ToString())
            .Order()
            .ToArray();
    }

    internal static HashSet<Guid> ParseMenuIds(string? menuIdsJson)
    {
        if (string.IsNullOrWhiteSpace(menuIdsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Guid[]>(menuIdsJson, JsonOptions)?.ToHashSet() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task<HashSet<Guid>> BuildValidMenuIdsWithAncestorsAsync(
        IReadOnlyList<Guid> menuIds,
        CancellationToken cancellationToken)
    {
        var selectedMenus = await dbContext.Menus
            .AsNoTracking()
            .Where(x => menuIds.Contains(x.Id) && x.IsEnabled)
            .ToArrayAsync(cancellationToken);
        var validMenuIds = selectedMenus.Select(x => x.Id).ToHashSet();
        var parentIds = selectedMenus
            .Select(x => x.ParentId)
            .OfType<Guid>()
            .Where(parentId => !validMenuIds.Contains(parentId))
            .ToHashSet();

        while (parentIds.Count > 0)
        {
            var parentMenus = await dbContext.Menus
                .AsNoTracking()
                .Where(x => parentIds.Contains(x.Id) && x.IsEnabled)
                .ToArrayAsync(cancellationToken);

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

        return validMenuIds;
    }

    private static TenantPackageDto ToDto(TenantPackage package)
    {
        return new TenantPackageDto(
            package.Id.ToString(),
            package.Name,
            package.MaxUsers,
            package.MaxStorageMb,
            ParseMenuIds(package.MenuIds).Count,
            package.IsEnabled,
            package.Remark);
    }

    private static string NormalizeRequired(string? value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
