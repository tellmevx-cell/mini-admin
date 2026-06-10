using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.SampleOrders;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfSampleOrderRepository(MiniAdminDbContext dbContext, ICurrentTenant currentTenant) : ISampleOrderRepository
{
    public async Task<PageResult<SampleOrderDto>> GetListAsync(SampleOrderListQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var source = dbContext.Set<SampleOrder>().AsNoTracking();

        source = ApplyTenantFilter(source);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            source = source.Where(entity =>
                entity.OriginalName.Contains(query.Keyword) ||
                entity.StoredName.Contains(query.Keyword) ||
                entity.ContentType.Contains(query.Keyword) ||
                entity.StorageProvider.Contains(query.Keyword) ||
                entity.StoragePath.Contains(query.Keyword) ||
                entity.Status.Contains(query.Keyword));
        }
        if (!string.IsNullOrWhiteSpace(query.OriginalName))
        {
            source = source.Where(entity => entity.OriginalName.Contains(query.OriginalName));
        }
        if (!string.IsNullOrWhiteSpace(query.StoredName))
        {
            source = source.Where(entity => entity.StoredName.Contains(query.StoredName));
        }
        if (!string.IsNullOrWhiteSpace(query.ContentType))
        {
            source = source.Where(entity => entity.ContentType.Contains(query.ContentType));
        }
        if (query.Size is not null)
        {
            source = source.Where(entity => entity.Size == query.Size);
        }
        if (!string.IsNullOrWhiteSpace(query.StorageProvider))
        {
            source = source.Where(entity => entity.StorageProvider.Contains(query.StorageProvider));
        }
        if (!string.IsNullOrWhiteSpace(query.StoragePath))
        {
            source = source.Where(entity => entity.StoragePath.Contains(query.StoragePath));
        }
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            source = source.Where(entity => entity.Status.Contains(query.Status));
        }
        var total = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(entity => ToDto(entity))
            .ToArrayAsync(cancellationToken);

        return new PageResult<SampleOrderDto>(items, total);
    }

    public async Task<SampleOrderDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyTenantFilter(dbContext.Set<SampleOrder>().AsNoTracking())
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<SampleOrderDto> CreateAsync(SaveSampleOrderRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new SampleOrder { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
        ApplyBusinessFields(entity, request);
        entity.Status = NormalizeInitialStatus(request.Status);

        entity.TenantId = currentTenant.TenantId;
        dbContext.Set<SampleOrder>().Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<SampleOrderDto?> UpdateAsync(Guid id, SaveSampleOrderRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyTenantFilter(dbContext.Set<SampleOrder>()).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        EnsureCanModify(entity);
        ApplyBusinessFields(entity, request);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyTenantFilter(dbContext.Set<SampleOrder>()).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        EnsureCanModify(entity);
        dbContext.Set<SampleOrder>().Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void ApplyBusinessFields(SampleOrder entity, SaveSampleOrderRequest request)
    {
        entity.OriginalName = request.OriginalName;
        entity.StoredName = request.StoredName;
        entity.ContentType = request.ContentType;
        entity.Size = request.Size;
        entity.StorageProvider = request.StorageProvider;
        entity.StoragePath = request.StoragePath;
    }

    private static SampleOrderDto ToDto(SampleOrder entity)
    {
        return new SampleOrderDto(
            entity.Id.ToString(),
            entity.WorkflowInstanceId?.ToString(),
            entity.OriginalName,
            entity.StoredName,
            entity.ContentType,
            entity.Size,
            entity.StorageProvider,
            entity.StoragePath,
            entity.Status,
            entity.CreatedAt);
    }

    private static string NormalizeInitialStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status)
            ? SampleOrder.DraftStatus
            : status.Trim();
    }

    private static void EnsureCanModify(SampleOrder entity)
    {
        if (entity.CanModify())
        {
            return;
        }

        throw new InvalidOperationException("审批中或已通过的示例订单不能编辑或删除.");
    }

    private IQueryable<SampleOrder> ApplyTenantFilter(IQueryable<SampleOrder> source)
    {
        return currentTenant.IsTenant
            ? source.Where(x => x.TenantId == currentTenant.TenantId)
            : source.Where(x => x.TenantId == null);
    }
}
