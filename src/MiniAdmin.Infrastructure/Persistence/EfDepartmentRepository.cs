using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Departments;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfDepartmentRepository(
    MiniAdminDbContext dbContext,
    ICurrentTenant currentTenant) : IDepartmentRepository
{
    public async Task<IReadOnlyList<DepartmentItemDto>> GetListAsync(
        CancellationToken cancellationToken = default)
    {
        var departments = await ApplyTenantScope(dbContext.Departments.AsNoTracking())
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Code)
            .ToListAsync(cancellationToken);
        var lookup = departments.ToLookup(x => x.ParentId);

        return lookup[null]
            .Select(department => ToDto(department, lookup))
            .ToArray();
    }

    public async Task<DepartmentItemDto> CreateAsync(
        SaveDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var department = new Department
        {
            Id = Guid.NewGuid(),
            TenantId = currentTenant.TenantId
        };

        await EnsureParentBelongsToCurrentTenantAsync(request.ParentId, cancellationToken);
        ApplyRequest(department, request);
        dbContext.Departments.Add(department);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(department, EmptyLookup);
    }

    public async Task<DepartmentItemDto?> UpdateAsync(
        Guid id,
        SaveDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var department = await ApplyTenantScope(dbContext.Departments)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (department is null)
        {
            return null;
        }

        await EnsureParentBelongsToCurrentTenantAsync(request.ParentId, cancellationToken);
        ApplyRequest(department, request);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(department, EmptyLookup);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var hasChildren = await ApplyTenantScope(dbContext.Departments)
            .AnyAsync(x => x.ParentId == id, cancellationToken);
        if (hasChildren)
        {
            return false;
        }

        var department = await ApplyTenantScope(dbContext.Departments)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (department is null)
        {
            return false;
        }

        dbContext.Departments.Remove(department);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static void ApplyRequest(Department department, SaveDepartmentRequest request)
    {
        department.ParentId = request.ParentId;
        department.Code = request.Code.Trim();
        department.Name = request.Name.Trim();
        department.Leader = NormalizeOptional(request.Leader);
        department.Phone = NormalizeOptional(request.Phone);
        department.Order = request.Order;
        department.IsEnabled = request.IsEnabled;
    }

    private IQueryable<Department> ApplyTenantScope(IQueryable<Department> departmentsQuery)
    {
        return currentTenant.IsTenant
            ? departmentsQuery.Where(x => x.TenantId == currentTenant.TenantId)
            : departmentsQuery.Where(x => x.TenantId == null);
    }

    private async Task EnsureParentBelongsToCurrentTenantAsync(
        Guid? parentId,
        CancellationToken cancellationToken)
    {
        if (!parentId.HasValue)
        {
            return;
        }

        var exists = await ApplyTenantScope(dbContext.Departments.AsNoTracking())
            .AnyAsync(x => x.Id == parentId.Value, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException("上级部门不存在或不属于当前租户.");
        }
    }

    private static DepartmentItemDto ToDto(Department department, ILookup<Guid?, Department> lookup)
    {
        var children = lookup[department.Id]
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Code)
            .Select(child => ToDto(child, lookup))
            .ToArray();

        return new DepartmentItemDto(
            department.Id.ToString(),
            department.ParentId?.ToString(),
            department.Code,
            department.Name,
            department.Leader,
            department.Phone,
            department.Order,
            department.IsEnabled,
            children);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static readonly ILookup<Guid?, Department> EmptyLookup = Array.Empty<Department>().ToLookup(x => x.ParentId);
}
