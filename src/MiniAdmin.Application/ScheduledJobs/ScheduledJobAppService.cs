using System.Diagnostics;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.ScheduledJobs;

namespace MiniAdmin.Application.ScheduledJobs;

public sealed class ScheduledJobAppService(
    IScheduledJobRepository scheduledJobRepository,
    IScheduledJobExecutor scheduledJobExecutor,
    IScheduledJobExecutionContext executionContext) : IScheduledJobAppService
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
        if (job is null)
        {
            return null;
        }

        var lease = await scheduledJobRepository.TryAcquireAsync(
            id,
            DateTimeOffset.UtcNow,
            executionContext.WorkerId,
            executionContext.LeaseDuration,
            requireDue: false,
            cancellationToken);
        return lease is null
            ? new ScheduledJobRunResultDto(
                job.Id,
                job.JobKey,
                "Skipped",
                "任务正在其他实例中执行，请稍后重试。",
                0)
            : await RunJobAsync(lease, "Manual", cancellationToken);
    }

    public async Task<int> RunDueJobsAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var executedCount = 0;
        var normalizedLimit = Math.Clamp(limit, 1, 20);
        while (executedCount < normalizedLimit)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var acquiredAt = executedCount == 0 ? now : DateTimeOffset.UtcNow;
            var lease = (await scheduledJobRepository.AcquireDueJobsAsync(
                    acquiredAt,
                    1,
                    executionContext.WorkerId,
                    executionContext.LeaseDuration,
                    cancellationToken))
                .SingleOrDefault();
            if (lease is null)
            {
                break;
            }

            // Acquire only when execution can begin so queued jobs never lose an idle lease.
            await RunJobAsync(lease, "Auto", cancellationToken);
            executedCount++;
        }

        return executedCount;
    }

    private async Task<ScheduledJobRunResultDto> RunJobAsync(
        ScheduledJobLease lease,
        string triggerType,
        CancellationToken cancellationToken)
    {
        var job = lease.Job;
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        ScheduledJobExecutionResult result;
        var leaseLost = false;
        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var heartbeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeatTask = MaintainLeaseAsync(
            lease,
            executionCancellation,
            heartbeatCancellation.Token,
            () => leaseLost = true);
        try
        {
            result = await scheduledJobExecutor.ExecuteAsync(job.JobKey, executionCancellation.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            heartbeatCancellation.Cancel();
            await AwaitHeartbeatAsync(heartbeatTask);
            await TryReleaseLeaseAsync(lease);
            throw;
        }
        catch (OperationCanceledException) when (leaseLost)
        {
            result = new ScheduledJobExecutionResult(
                "LeaseLost",
                "任务租约已丢失，当前实例停止提交执行结果。请检查任务是否运行超过租约或数据库连接是否中断。");
        }
        catch (Exception exception)
        {
            result = new ScheduledJobExecutionResult("Failed", exception.Message);
        }
        finally
        {
            heartbeatCancellation.Cancel();
        }

        await AwaitHeartbeatAsync(heartbeatTask);

        stopwatch.Stop();
        var finishedAt = DateTimeOffset.UtcNow;
        var recorded = await scheduledJobRepository.RecordExecutionAsync(
            Guid.Parse(job.Id),
            lease.LeaseToken,
            new ScheduledJobExecutionRecord(
                triggerType,
                result.Status,
                result.Message,
                startedAt,
                finishedAt,
                stopwatch.ElapsedMilliseconds,
                result.Details),
            cancellationToken);

        if (!recorded)
        {
            return new ScheduledJobRunResultDto(
                job.Id,
                job.JobKey,
                "LeaseLost",
                "任务执行已结束，但租约不再属于当前实例，结果未覆盖新实例状态。",
                stopwatch.ElapsedMilliseconds);
        }

        return new ScheduledJobRunResultDto(
            job.Id,
            job.JobKey,
            result.Status,
            result.Message,
            stopwatch.ElapsedMilliseconds);
    }

    private async Task MaintainLeaseAsync(
        ScheduledJobLease lease,
        CancellationTokenSource executionCancellation,
        CancellationToken cancellationToken,
        Action onLeaseLost)
    {
        var validUntil = lease.LeaseExpiresAt;
        using var timer = new PeriodicTimer(executionContext.HeartbeatInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    var now = DateTimeOffset.UtcNow;
                    var renewed = await scheduledJobRepository.RenewLeaseAsync(
                        Guid.Parse(lease.Job.Id),
                        lease.LeaseToken,
                        now,
                        executionContext.LeaseDuration,
                        cancellationToken);
                    if (!renewed)
                    {
                        onLeaseLost();
                        executionCancellation.Cancel();
                        return;
                    }

                    validUntil = now.Add(executionContext.LeaseDuration);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch
                {
                    if (DateTimeOffset.UtcNow < validUntil)
                    {
                        continue;
                    }

                    onLeaseLost();
                    executionCancellation.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal completion or host shutdown stops the heartbeat loop.
        }
    }

    private async Task TryReleaseLeaseAsync(ScheduledJobLease lease)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await scheduledJobRepository.ReleaseLeaseAsync(
                Guid.Parse(lease.Job.Id),
                lease.LeaseToken,
                timeout.Token);
        }
        catch
        {
            // The lease has a finite expiry and can be recovered by another instance.
        }
    }

    private static async Task AwaitHeartbeatAsync(Task heartbeatTask)
    {
        try
        {
            await heartbeatTask;
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected when execution completes.
        }
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
