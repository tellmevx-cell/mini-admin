using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.ScheduledJobs;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfScheduledJobRepository(MiniAdminDbContext dbContext) : IScheduledJobRepository
{
    public async Task<PageResult<ScheduledJobDto>> GetListAsync(
        ScheduledJobListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var jobsQuery = ApplyFilters(dbContext.ScheduledJobs.AsNoTracking(), query);

        var total = await jobsQuery.CountAsync(cancellationToken);
        var items = await jobsQuery
            .OrderBy(x => x.JobKey)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToDto(x))
            .ToArrayAsync(cancellationToken);

        return new PageResult<ScheduledJobDto>(items, total);
    }

    public async Task<ScheduledJobDto?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.ScheduledJobs
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => ToDto(x))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<PageResult<ScheduledJobLogDto>> GetLogsAsync(
        Guid jobId,
        ScheduledJobLogListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var logsQuery = dbContext.ScheduledJobLogs
            .AsNoTracking()
            .Where(x => x.JobId == jobId);

        var total = await logsQuery.CountAsync(cancellationToken);
        var items = await logsQuery
            .OrderByDescending(x => x.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToDto(x))
            .ToArrayAsync(cancellationToken);

        return new PageResult<ScheduledJobLogDto>(items, total);
    }

    public async Task<PageResult<ScheduledJobLogDetailDto>> GetLogDetailsAsync(
        Guid logId,
        ScheduledJobLogDetailListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var detailsQuery = dbContext.ScheduledJobLogDetails
            .AsNoTracking()
            .Where(x => x.LogId == logId);

        var total = await detailsQuery.CountAsync(cancellationToken);
        var items = await detailsQuery
            .OrderBy(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToDto(x))
            .ToArrayAsync(cancellationToken);

        return new PageResult<ScheduledJobLogDetailDto>(items, total);
    }

    public async Task<ScheduledJobDto?> UpdateAsync(
        Guid id,
        SaveScheduledJobRequest request,
        CancellationToken cancellationToken = default)
    {
        var job = await dbContext.ScheduledJobs.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (job is null)
        {
            return null;
        }

        job.Name = request.Name;
        job.Description = request.Description;
        job.IntervalSeconds = request.IntervalSeconds;
        job.IsEnabled = request.IsEnabled;
        job.UpdatedAt = DateTimeOffset.UtcNow;
        job.NextRunAt = request.IsEnabled ? DateTimeOffset.UtcNow.AddSeconds(job.IntervalSeconds) : null;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(job);
    }

    public async Task<IReadOnlyList<ScheduledJobDto>> GetDueJobsAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.ScheduledJobs
            .AsNoTracking()
            .Where(x => x.IsEnabled && x.NextRunAt != null && x.NextRunAt <= now)
            .OrderBy(x => x.NextRunAt)
            .Take(Math.Clamp(limit, 1, 20))
            .Select(x => ToDto(x))
            .ToArrayAsync(cancellationToken);
    }

    public async Task RecordExecutionAsync(
        Guid jobId,
        ScheduledJobExecutionRecord record,
        CancellationToken cancellationToken = default)
    {
        var job = await dbContext.ScheduledJobs.SingleAsync(x => x.Id == jobId, cancellationToken);
        job.LastRunAt = record.StartedAt;
        job.LastStatus = record.Status;
        job.LastMessage = record.Message;
        job.NextRunAt = job.IsEnabled ? record.FinishedAt.AddSeconds(job.IntervalSeconds) : null;
        job.UpdatedAt = DateTimeOffset.UtcNow;

        var log = new ScheduledJobLog
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            JobKey = job.JobKey,
            JobName = job.Name,
            TriggerType = record.TriggerType,
            Status = record.Status,
            Message = record.Message,
            StartedAt = record.StartedAt,
            FinishedAt = record.FinishedAt,
            ElapsedMilliseconds = record.ElapsedMilliseconds
        };

        dbContext.ScheduledJobLogs.Add(log);

        foreach (var detail in record.Details ?? [])
        {
            dbContext.ScheduledJobLogDetails.Add(new ScheduledJobLogDetail
            {
                Id = Guid.NewGuid(),
                LogId = log.Id,
                JobId = job.Id,
                JobKey = job.JobKey,
                DetailType = detail.DetailType,
                TargetType = detail.TargetType,
                TargetId = detail.TargetId,
                TargetName = detail.TargetName,
                StorageProvider = detail.StorageProvider,
                StoragePath = detail.StoragePath,
                Status = detail.Status,
                Message = detail.Message,
                CreatedAt = record.FinishedAt
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<ScheduledJob> ApplyFilters(
        IQueryable<ScheduledJob> queryable,
        ScheduledJobListQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.JobKey))
        {
            queryable = queryable.Where(x => x.JobKey.Contains(query.JobKey));
        }

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            queryable = queryable.Where(x => x.Name.Contains(query.Name));
        }

        if (query.IsEnabled.HasValue)
        {
            queryable = queryable.Where(x => x.IsEnabled == query.IsEnabled.Value);
        }

        return queryable;
    }

    private static ScheduledJobDto ToDto(ScheduledJob job)
    {
        return new ScheduledJobDto(
            job.Id.ToString(),
            job.JobKey,
            job.Name,
            job.Description,
            job.IntervalSeconds,
            job.IsEnabled,
            job.LastStatus,
            job.LastMessage,
            job.LastRunAt,
            job.NextRunAt);
    }

    private static ScheduledJobLogDto ToDto(ScheduledJobLog log)
    {
        return new ScheduledJobLogDto(
            log.Id.ToString(),
            log.JobId.ToString(),
            log.JobKey,
            log.JobName,
            log.TriggerType,
            log.Status,
            log.Message,
            log.StartedAt,
            log.FinishedAt,
            log.ElapsedMilliseconds);
    }

    private static ScheduledJobLogDetailDto ToDto(ScheduledJobLogDetail detail)
    {
        return new ScheduledJobLogDetailDto(
            detail.Id.ToString(),
            detail.LogId.ToString(),
            detail.JobId.ToString(),
            detail.JobKey,
            detail.DetailType,
            detail.TargetType,
            detail.TargetId,
            detail.TargetName,
            detail.StorageProvider,
            detail.StoragePath,
            detail.Status,
            detail.Message,
            detail.CreatedAt);
    }
}
