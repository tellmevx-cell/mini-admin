using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.Customers;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfCustomerRepository(MiniAdminDbContext dbContext, ICurrentTenant currentTenant) : ICustomerRepository
{
    public async Task<PageResult<CustomerDto>> GetListAsync(CustomerListQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var source = dbContext.Set<Customer>().AsNoTracking();

        source = ApplyTenantFilter(source);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            source = source.Where(entity =>
                entity.Title.Contains(query.Keyword) ||
                entity.Type.Contains(query.Keyword) ||
                entity.Content.Contains(query.Keyword));
        }

        var total = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(entity => ToDto(entity))
            .ToArrayAsync(cancellationToken);

        return new PageResult<CustomerDto>(items, total);
    }

    public async Task<CustomerDto> CreateAsync(SaveCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new Customer { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
        Apply(entity, request);

        entity.TenantId = currentTenant.TenantId;
        dbContext.Set<Customer>().Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<CustomerDto?> UpdateAsync(Guid id, SaveCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyTenantFilter(dbContext.Set<Customer>()).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
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
        var entity = await ApplyTenantFilter(dbContext.Set<Customer>()).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        dbContext.Set<Customer>().Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void Apply(Customer entity, SaveCustomerRequest request)
    {
        entity.Title = request.Title;
        entity.Type = request.Type;
        entity.Content = request.Content;
        entity.IsPublished = request.IsPublished;
        entity.PublishedAt = request.PublishedAt;
    }

    private static CustomerDto ToDto(Customer entity)
    {
        return new CustomerDto(
            entity.Id.ToString(),
            entity.Title,
            entity.Type,
            entity.Content,
            entity.IsPublished,
            entity.PublishedAt,
            entity.CreatedAt);
    }

    private IQueryable<Customer> ApplyTenantFilter(IQueryable<Customer> source)
    {
        return currentTenant.IsTenant
            ? source.Where(x => x.TenantId == currentTenant.TenantId)
            : source.Where(x => x.TenantId == null);
    }
}
