using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.ScheduledJobs;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfScheduledJobRepository(
    MiniAdminDbContext dbContext,
    DbContextOptions<MiniAdminDbContext> dbContextOptions) : IScheduledJobRepository
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

    public async Task<IReadOnlyList<ScheduledJobLease>> AcquireDueJobsAsync(
        DateTimeOffset now,
        int limit,
        string leaseOwner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        var candidateIds = await dbContext.ScheduledJobs
            .AsNoTracking()
            .Where(x =>
                x.IsEnabled &&
                x.NextRunAt != null &&
                x.NextRunAt <= now &&
                (x.LeaseExpiresAt == null || x.LeaseExpiresAt <= now))
            .OrderBy(x => x.NextRunAt)
            .Take(Math.Clamp(limit, 1, 20))
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);

        var leases = new List<ScheduledJobLease>(candidateIds.Length);
        foreach (var candidateId in candidateIds)
        {
            var lease = await TryAcquireAsync(
                candidateId,
                now,
                leaseOwner,
                leaseDuration,
                requireDue: true,
                cancellationToken);
            if (lease is not null)
            {
                leases.Add(lease);
            }
        }

        return leases;
    }

    public async Task<ScheduledJobLease?> TryAcquireAsync(
        Guid id,
        DateTimeOffset now,
        string leaseOwner,
        TimeSpan leaseDuration,
        bool requireDue,
        CancellationToken cancellationToken = default)
    {
        var leaseToken = Guid.NewGuid();
        var leaseExpiresAt = now.Add(leaseDuration);

        if (dbContext.Database.IsRelational())
        {
            var query = dbContext.ScheduledJobs.Where(x =>
                x.Id == id &&
                (x.LeaseExpiresAt == null || x.LeaseExpiresAt <= now));
            if (requireDue)
            {
                query = query.Where(x =>
                    x.IsEnabled &&
                    x.NextRunAt != null &&
                    x.NextRunAt <= now);
            }

            var affected = await query.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.LeaseToken, (Guid?)leaseToken)
                    .SetProperty(x => x.LeaseOwner, leaseOwner)
                    .SetProperty(x => x.LeaseExpiresAt, (DateTimeOffset?)leaseExpiresAt)
                    .SetProperty(x => x.LastHeartbeatAt, (DateTimeOffset?)now)
                    .SetProperty(x => x.UpdatedAt, now),
                cancellationToken);
            if (affected != 1)
            {
                return null;
            }
        }
        else
        {
            var job = await dbContext.ScheduledJobs.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (job is null ||
                (job.LeaseExpiresAt.HasValue && job.LeaseExpiresAt > now) ||
                (requireDue && (!job.IsEnabled || !job.NextRunAt.HasValue || job.NextRunAt > now)))
            {
                return null;
            }

            job.LeaseToken = leaseToken;
            job.LeaseOwner = leaseOwner;
            job.LeaseExpiresAt = leaseExpiresAt;
            job.LastHeartbeatAt = now;
            job.UpdatedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var leasedJob = await dbContext.ScheduledJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == id && x.LeaseToken == leaseToken,
                cancellationToken);

        return leasedJob is null
            ? null
            : new ScheduledJobLease(ToDto(leasedJob), leaseToken, leaseOwner, leaseExpiresAt);
    }

    public async Task<bool> RenewLeaseAsync(
        Guid jobId,
        Guid leaseToken,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        await using var leaseDbContext = new MiniAdminDbContext(dbContextOptions);
        if (leaseDbContext.Database.IsRelational())
        {
            var affected = await leaseDbContext.ScheduledJobs
                .Where(x => x.Id == jobId && x.LeaseToken == leaseToken)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.LeaseExpiresAt, (DateTimeOffset?)now.Add(leaseDuration))
                        .SetProperty(x => x.LastHeartbeatAt, (DateTimeOffset?)now)
                        .SetProperty(x => x.UpdatedAt, now),
                    cancellationToken);
            return affected == 1;
        }

        var job = await leaseDbContext.ScheduledJobs.SingleOrDefaultAsync(
            x => x.Id == jobId && x.LeaseToken == leaseToken,
            cancellationToken);
        if (job is null)
        {
            return false;
        }

        job.LeaseExpiresAt = now.Add(leaseDuration);
        job.LastHeartbeatAt = now;
        job.UpdatedAt = now;
        await leaseDbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReleaseLeaseAsync(
        Guid jobId,
        Guid leaseToken,
        CancellationToken cancellationToken = default)
    {
        await using var leaseDbContext = new MiniAdminDbContext(dbContextOptions);
        if (leaseDbContext.Database.IsRelational())
        {
            var affected = await leaseDbContext.ScheduledJobs
                .Where(x => x.Id == jobId && x.LeaseToken == leaseToken)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.LeaseToken, (Guid?)null)
                        .SetProperty(x => x.LeaseOwner, (string?)null)
                        .SetProperty(x => x.LeaseExpiresAt, (DateTimeOffset?)null)
                        .SetProperty(x => x.LastHeartbeatAt, (DateTimeOffset?)null)
                        .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow),
                    cancellationToken);
            return affected == 1;
        }

        var job = await leaseDbContext.ScheduledJobs.SingleOrDefaultAsync(
            x => x.Id == jobId && x.LeaseToken == leaseToken,
            cancellationToken);
        if (job is null)
        {
            return false;
        }

        ClearLease(job);
        job.UpdatedAt = DateTimeOffset.UtcNow;
        await leaseDbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RecordExecutionAsync(
        Guid jobId,
        Guid leaseToken,
        ScheduledJobExecutionRecord record,
        CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.IsRelational())
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var jobSnapshot = await dbContext.ScheduledJobs
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == jobId && x.LeaseToken == leaseToken, cancellationToken);
            if (jobSnapshot is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            var affected = await dbContext.ScheduledJobs
                .Where(x => x.Id == jobId && x.LeaseToken == leaseToken)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.LastRunAt, (DateTimeOffset?)record.StartedAt)
                        .SetProperty(x => x.LastStatus, record.Status)
                        .SetProperty(x => x.LastMessage, record.Message)
                        .SetProperty(
                            x => x.NextRunAt,
                            jobSnapshot.IsEnabled
                                ? (DateTimeOffset?)record.FinishedAt.AddSeconds(jobSnapshot.IntervalSeconds)
                                : null)
                        .SetProperty(x => x.LeaseToken, (Guid?)null)
                        .SetProperty(x => x.LeaseOwner, (string?)null)
                        .SetProperty(x => x.LeaseExpiresAt, (DateTimeOffset?)null)
                        .SetProperty(x => x.LastHeartbeatAt, (DateTimeOffset?)null)
                        .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow),
                    cancellationToken);
            if (affected != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            AddExecutionLog(jobSnapshot, record);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        var job = await dbContext.ScheduledJobs.SingleOrDefaultAsync(
            x => x.Id == jobId && x.LeaseToken == leaseToken,
            cancellationToken);
        if (job is null)
        {
            return false;
        }

        job.LastRunAt = record.StartedAt;
        job.LastStatus = record.Status;
        job.LastMessage = record.Message;
        job.NextRunAt = job.IsEnabled ? record.FinishedAt.AddSeconds(job.IntervalSeconds) : null;
        ClearLease(job);
        job.UpdatedAt = DateTimeOffset.UtcNow;

        AddExecutionLog(job, record);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private void AddExecutionLog(ScheduledJob job, ScheduledJobExecutionRecord record)
    {
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
    }

    private static void ClearLease(ScheduledJob job)
    {
        job.LeaseToken = null;
        job.LeaseOwner = null;
        job.LeaseExpiresAt = null;
        job.LastHeartbeatAt = null;
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
