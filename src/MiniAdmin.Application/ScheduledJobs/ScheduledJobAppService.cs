using System.Diagnostics;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.ScheduledJobs;

namespace MiniAdmin.Application.ScheduledJobs;

public sealed class ScheduledJobAppService(
    IScheduledJobRepository scheduledJobRepository,
    IScheduledJobExecutor scheduledJobExecutor) : IScheduledJobAppService
{
    public Task<PageResult<ScheduledJobDto>> GetListAsync(
        ScheduledJobListQuery query,
        CancellationToken cancellationToken = default)
    {
        return scheduledJobRepository.GetListAsync(query, cancellationToken);
    }

    public Task<ScheduledJobDto?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return scheduledJobRepository.GetAsync(id, cancellationToken);
    }

    public Task<PageResult<ScheduledJobLogDto>> GetLogsAsync(
        Guid jobId,
        ScheduledJobLogListQuery query,
        CancellationToken cancellationToken = default)
    {
        return scheduledJobRepository.GetLogsAsync(jobId, query, cancellationToken);
    }

    public Task<PageResult<ScheduledJobLogDetailDto>> GetLogDetailsAsync(
        Guid logId,
        ScheduledJobLogDetailListQuery query,
        CancellationToken cancellationToken = default)
    {
        return scheduledJobRepository.GetLogDetailsAsync(logId, query, cancellationToken);
    }

    public Task<ScheduledJobDto?> UpdateAsync(
        Guid id,
        SaveScheduledJobRequest request,
        CancellationToken cancellationToken = default)
    {
        return scheduledJobRepository.UpdateAsync(id, NormalizeRequest(request), cancellationToken);
    }

    public async Task<ScheduledJobRunResultDto?> RunOnceAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var job = await scheduledJobRepository.GetAsync(id, cancellationToken);
        return job is null
            ? null
            : await RunJobAsync(job, "Manual", cancellationToken);
    }

    public async Task<int> RunDueJobsAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var jobs = await scheduledJobRepository.GetDueJobsAsync(now, Math.Clamp(limit, 1, 20), cancellationToken);
        foreach (var job in jobs)
        {
            await RunJobAsync(job, "Auto", cancellationToken);
        }

        return jobs.Count;
    }

    private async Task<ScheduledJobRunResultDto> RunJobAsync(
        ScheduledJobDto job,
        string triggerType,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        ScheduledJobExecutionResult result;
        try
        {
            result = await scheduledJobExecutor.ExecuteAsync(job.JobKey, cancellationToken);
        }
        catch (Exception exception)
        {
            result = new ScheduledJobExecutionResult("Failed", exception.Message);
        }

        stopwatch.Stop();
        var finishedAt = DateTimeOffset.UtcNow;
        await scheduledJobRepository.RecordExecutionAsync(
            Guid.Parse(job.Id),
            new ScheduledJobExecutionRecord(
                triggerType,
                result.Status,
                result.Message,
                startedAt,
                finishedAt,
                stopwatch.ElapsedMilliseconds,
                result.Details),
            cancellationToken);

        return new ScheduledJobRunResultDto(
            job.Id,
            job.JobKey,
            result.Status,
            result.Message,
            stopwatch.ElapsedMilliseconds);
    }

    private static SaveScheduledJobRequest NormalizeRequest(SaveScheduledJobRequest request)
    {
        return request with
        {
            Name = string.IsNullOrWhiteSpace(request.Name) ? "未命名任务" : request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IntervalSeconds = Math.Max(request.IntervalSeconds, 60)
        };
    }
}
