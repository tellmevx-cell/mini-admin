using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.Efmigrationshistorys;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfEfmigrationshistoryRepository(MiniAdminDbContext dbContext, ICurrentTenant currentTenant) : IEfmigrationshistoryRepository
{
    public async Task<PageResult<EfmigrationshistoryDto>> GetListAsync(EfmigrationshistoryListQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var source = dbContext.Set<Efmigrationshistory>().AsNoTracking();

        source = ApplyTenantFilter(source);


        if (!string.IsNullOrWhiteSpace(query.ProductVersion))
        {
            source = source.Where(entity => entity.ProductVersion.Contains(query.ProductVersion));
        }
        var total = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(entity => ToDto(entity))
            .ToArrayAsync(cancellationToken);

        return new PageResult<EfmigrationshistoryDto>(items, total);
    }

    public async Task<IReadOnlyList<EfmigrationshistoryDto>> GetExportListAsync(EfmigrationshistoryListQuery query, int limit = 10000, CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 10000);
        var source = dbContext.Set<Efmigrationshistory>().AsNoTracking();

        source = ApplyTenantFilter(source);


        if (!string.IsNullOrWhiteSpace(query.ProductVersion))
        {
            source = source.Where(entity => entity.ProductVersion.Contains(query.ProductVersion));
        }
        return await source
            .OrderByDescending(entity => entity.CreatedAt)
            .Take(take)
            .Select(entity => ToDto(entity))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<EfmigrationshistoryDto> CreateAsync(SaveEfmigrationshistoryRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new Efmigrationshistory { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
        Apply(entity, request);

        entity.TenantId = currentTenant.TenantId;
        dbContext.Set<Efmigrationshistory>().Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<EfmigrationshistoryDto?> UpdateAsync(Guid id, SaveEfmigrationshistoryRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyTenantFilter(dbContext.Set<Efmigrationshistory>()).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        Apply(entity, request);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyTenantFilter(dbContext.Set<Efmigrationshistory>()).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        dbContext.Set<Efmigrationshistory>().Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<EfmigrationshistoryDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyTenantFilter(dbContext.Set<Efmigrationshistory>()).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<EfmigrationshistoryDto?> SetWorkflowStateAsync(Guid id, string approvalStatus, string? workflowInstanceId, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyTenantFilter(dbContext.Set<Efmigrationshistory>()).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.ApprovalStatus = approvalStatus;
        entity.WorkflowInstanceId = workflowInstanceId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    private static void Apply(Efmigrationshistory entity, SaveEfmigrationshistoryRequest request)
    {
        entity.ProductVersion = request.ProductVersion;
    }

    private static EfmigrationshistoryDto ToDto(Efmigrationshistory entity)
    {
        return new EfmigrationshistoryDto(
            entity.Id.ToString(),
            entity.WorkflowInstanceId,
            entity.ApprovalStatus,
            entity.ProductVersion,
            entity.CreatedAt);
    }

    private IQueryable<Efmigrationshistory> ApplyTenantFilter(IQueryable<Efmigrationshistory> source)
    {
        return currentTenant.IsTenant
            ? source.Where(x => x.TenantId == currentTenant.TenantId)
            : source.Where(x => x.TenantId == null);
    }

}