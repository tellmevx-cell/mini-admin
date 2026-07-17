using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Files;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfFileRepository(
    MiniAdminDbContext dbContext,
    ICurrentTenant currentTenant) : IFileRepository
{
    public async Task<PageResult<FileDto>> GetListAsync(
        FileListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var filesQuery = ApplyTenantScope(dbContext.ManagedFiles.AsNoTracking());

        if (!string.IsNullOrWhiteSpace(query.OriginalName))
        {
            filesQuery = filesQuery.Where(x => x.OriginalName.Contains(query.OriginalName));
        }

        if (!string.IsNullOrWhiteSpace(query.StorageProvider))
        {
            filesQuery = filesQuery.Where(x => x.StorageProvider == query.StorageProvider);
        }

        var total = await filesQuery.CountAsync(cancellationToken);
        var items = await filesQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToDto(x))
            .ToArrayAsync(cancellationToken);

        return new PageResult<FileDto>(items, total);
    }

    public async Task<FileDto?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await ApplyTenantScope(dbContext.ManagedFiles.AsNoTracking())
            .Where(x => x.Id == id)
            .Select(x => ToDto(x))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<FileDto> CreateAsync(
        CreateFileRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        var file = new ManagedFile
        {
            Id = Guid.NewGuid(),
            TenantId = currentTenant.TenantId,
            OriginalName = request.OriginalName,
            StoredName = request.StoredName,
            ContentType = request.ContentType,
            Size = request.Size,
            StorageProvider = request.StorageProvider,
            StoragePath = request.StoragePath,
            Status = request.Status,
            CreatedAt = request.CreatedAt
        };

        dbContext.ManagedFiles.Add(file);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(file);
    }

    public async Task<FileDto?> MarkInvalidAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var file = await ApplyTenantScope(dbContext.ManagedFiles)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (file is null)
        {
            return null;
        }

        file.Status = "Invalid";
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(file);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var file = await ApplyTenantScope(dbContext.ManagedFiles)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (file is null)
        {
            return false;
        }

        dbContext.ManagedFiles.Remove(file);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static FileDto ToDto(ManagedFile file)
    {
        return new FileDto(
            file.Id.ToString(),
            file.OriginalName,
            file.StoredName,
            file.ContentType,
            file.Size,
            file.StorageProvider,
            file.StoragePath,
            file.Status,
            file.CreatedAt);
    }

    private IQueryable<ManagedFile> ApplyTenantScope(IQueryable<ManagedFile> filesQuery)
    {
        return currentTenant.IsTenant
            ? filesQuery.Where(x => x.TenantId == currentTenant.TenantId)
            : filesQuery;
    }
}
