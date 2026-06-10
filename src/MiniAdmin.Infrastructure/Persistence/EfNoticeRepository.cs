using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Notices;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfNoticeRepository(MiniAdminDbContext dbContext) : INoticeRepository
{
    public async Task<PageResult<NoticeDto>> GetListAsync(
        NoticeListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var noticesQuery = dbContext.Notices.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Title))
        {
            noticesQuery = noticesQuery.Where(x => x.Title.Contains(query.Title));
        }

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            noticesQuery = noticesQuery.Where(x => x.Type == query.Type);
        }

        if (query.IsPublished.HasValue)
        {
            noticesQuery = noticesQuery.Where(x => x.IsPublished == query.IsPublished);
        }

        var total = await noticesQuery.CountAsync(cancellationToken);
        var items = await noticesQuery
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToDto(x))
            .ToArrayAsync(cancellationToken);

        return new PageResult<NoticeDto>(items, total);
    }

    public async Task<NoticeDto> CreateAsync(
        SaveNoticeRequest request,
        CancellationToken cancellationToken = default)
    {
        var notice = new Notice
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        ApplyRequest(notice, request);
        dbContext.Notices.Add(notice);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(notice);
    }

    public async Task<NoticeDto?> UpdateAsync(
        Guid id,
        SaveNoticeRequest request,
        CancellationToken cancellationToken = default)
    {
        var notice = await dbContext.Notices.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (notice is null)
        {
            return null;
        }

        ApplyRequest(notice, request);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(notice);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notice = await dbContext.Notices.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (notice is null)
        {
            return false;
        }

        dbContext.Notices.Remove(notice);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static void ApplyRequest(Notice notice, SaveNoticeRequest request)
    {
        notice.Title = request.Title.Trim();
        notice.Type = request.Type.Trim();
        notice.Content = request.Content.Trim();
        notice.IsPublished = request.IsPublished;
        notice.PublishedAt = request.IsPublished ? DateTimeOffset.UtcNow : null;
    }

    private static NoticeDto ToDto(Notice notice)
    {
        return new NoticeDto(
            notice.Id.ToString(),
            notice.Title,
            notice.Type,
            notice.Content,
            notice.IsPublished,
            notice.PublishedAt,
            notice.CreatedAt);
    }
}
