using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using MiniAdmin.Application.Contracts.DataScopes;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfDataScopeProvider(MiniAdminDbContext dbContext) : IDataScopeProvider
{
    public async Task<DataScopeContext> GetAsync(
        string? currentUserName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentUserName))
        {
            return DataScopeContext.Unrestricted();
        }

        var currentUser = await dbContext.Users
            .AsNoTracking()
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.UserName == currentUserName && x.IsEnabled, cancellationToken);
        if (currentUser is null)
        {
            return DataScopeContext.Denied();
        }

        var roles = currentUser.UserRoles
            .Select(x => x.Role)
            .Where(x => x.IsEnabled)
            .ToArray();

        if (roles.Any(x => string.Equals(x.DataScope, "all", StringComparison.OrdinalIgnoreCase)))
        {
            return new DataScopeContext(
                DataScopeLevel.All,
                currentUser.Id,
                currentUser.UserName,
                currentUser.DepartmentId,
                new HashSet<Guid>());
        }

        var departmentIds = new HashSet<Guid>();
        var hasDepartmentAndChildren = false;
        var hasDepartment = false;
        var hasCustomDepartments = false;

        if (currentUser.DepartmentId is Guid currentDepartmentId)
        {
            if (roles.Any(x => string.Equals(x.DataScope, "department-and-children", StringComparison.OrdinalIgnoreCase)))
            {
                hasDepartmentAndChildren = true;
                foreach (var departmentId in await GetDepartmentAndChildrenIdsAsync(currentDepartmentId, cancellationToken))
                {
                    departmentIds.Add(departmentId);
                }
            }

            if (roles.Any(x => string.Equals(x.DataScope, "department", StringComparison.OrdinalIgnoreCase)))
            {
                hasDepartment = true;
                departmentIds.Add(currentDepartmentId);
            }
        }

        var customDepartmentIds = ParseCustomDepartmentIds(roles);
        if (customDepartmentIds.Count > 0)
        {
            hasCustomDepartments = true;
            foreach (var departmentId in await dbContext.Departments
                         .AsNoTracking()
                         .Where(x => customDepartmentIds.Contains(x.Id))
                         .Select(x => x.Id)
                         .ToArrayAsync(cancellationToken))
            {
                departmentIds.Add(departmentId);
            }
        }

        if (departmentIds.Count == 0)
        {
            return new DataScopeContext(
                DataScopeLevel.Self,
                currentUser.Id,
                currentUser.UserName,
                currentUser.DepartmentId,
                new HashSet<Guid>());
        }

        return new DataScopeContext(
            ResolveLevel(
                hasDepartmentAndChildren,
                hasDepartment,
                hasCustomDepartments,
                currentUser.DepartmentId,
                departmentIds),
            currentUser.Id,
            currentUser.UserName,
            currentUser.DepartmentId,
            departmentIds);
    }

    private async Task<IReadOnlySet<Guid>> GetDepartmentAndChildrenIdsAsync(
        Guid departmentId,
        CancellationToken cancellationToken)
    {
        var departments = await dbContext.Departments
            .AsNoTracking()
            .Select(x => new { x.Id, x.ParentId })
            .ToArrayAsync(cancellationToken);
        var departmentIds = new HashSet<Guid> { departmentId };
        var added = true;

        while (added)
        {
            added = false;
            foreach (var department in departments)
            {
                if (department.ParentId is Guid parentId &&
                    departmentIds.Contains(parentId) &&
                    departmentIds.Add(department.Id))
                {
                    added = true;
                }
            }
        }

        return departmentIds;
    }

    private static HashSet<Guid> ParseCustomDepartmentIds(IEnumerable<Domain.Entities.Role> roles)
    {
        var result = new HashSet<Guid>();

        foreach (var role in roles)
        {
            if (!string.Equals(role.DataScope, "custom", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(role.CustomDepartmentIds))
            {
                continue;
            }

            try
            {
                var ids = JsonSerializer.Deserialize<Guid[]>(role.CustomDepartmentIds);
                if (ids is null)
                {
                    continue;
                }

                foreach (var id in ids)
                {
                    result.Add(id);
                }
            }
            catch (JsonException)
            {
                // Ignore malformed historical values and continue.
            }
        }

        return result;
    }

    private static DataScopeLevel ResolveLevel(
        bool hasDepartmentAndChildren,
        bool hasDepartment,
        bool hasCustomDepartments,
        Guid? currentDepartmentId,
        IReadOnlySet<Guid> departmentIds)
    {
        if (hasCustomDepartments && (hasDepartmentAndChildren || hasDepartment))
        {
            return DataScopeLevel.Mixed;
        }

        if (hasCustomDepartments)
        {
            return DataScopeLevel.CustomDepartments;
        }

        if (hasDepartmentAndChildren)
        {
            return currentDepartmentId.HasValue && departmentIds.Contains(currentDepartmentId.Value)
                ? DataScopeLevel.DepartmentAndChildren
                : DataScopeLevel.Mixed;
        }

        if (hasDepartment)
        {
            return DataScopeLevel.Department;
        }

        return DataScopeLevel.Self;
    }
}
