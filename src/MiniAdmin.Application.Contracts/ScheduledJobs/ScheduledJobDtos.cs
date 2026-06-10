using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.ScheduledJobs;

public sealed record ScheduledJobListQuery(
    int Page = 1,
    int PageSize = 20,
    string? JobKey = null,
    string? Name = null,
    bool? IsEnabled = null);

public sealed record ScheduledJobLogListQuery(
    int Page = 1,
    int PageSize = 20);

public sealed record ScheduledJobLogDetailListQuery(
    int Page = 1,
    int PageSize = 20);

public sealed record ScheduledJobDto(
    string Id,
    string JobKey,
    string Name,
    string? Description,
    int IntervalSeconds,
    bool IsEnabled,
    string LastStatus,
    string? LastMessage,
    DateTimeOffset? LastRunAt,
    DateTimeOffset? NextRunAt);

public sealed record ScheduledJobLogDto(
    string Id,
    string JobId,
    string JobKey,
    string JobName,
    string TriggerType,
    string Status,
    string Message,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    long ElapsedMilliseconds);

public sealed record ScheduledJobLogDetailDto(
    string Id,
    string LogId,
    string JobId,
    string JobKey,
    string DetailType,
    string TargetType,
    string? TargetId,
    string? TargetName,
    string? StorageProvider,
    string? StoragePath,
    string Status,
    string Message,
    DateTimeOffset CreatedAt);

public sealed record SaveScheduledJobRequest(
    string Name,
    string? Description,
    int IntervalSeconds,
    bool IsEnabled);

public sealed record ScheduledJobExecutionDetail(
    string DetailType,
    string TargetType,
    string? TargetId,
    string? TargetName,
    string? StorageProvider,
    string? StoragePath,
    string Status,
    string Message);

public sealed record ScheduledJobExecutionResult(
    string Status,
    string Message,
    IReadOnlyList<ScheduledJobExecutionDetail>? Details = null);

public sealed record ScheduledJobExecutionRecord(
    string TriggerType,
    string Status,
    string Message,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    long ElapsedMilliseconds,
    IReadOnlyList<ScheduledJobExecutionDetail>? Details = null);

public sealed record ScheduledJobRunResultDto(
    string JobId,
    string JobKey,
    string Status,
    string Message,
    long ElapsedMilliseconds);

public interface IScheduledJobRepository
{
    Task<PageResult<ScheduledJobDto>> GetListAsync(
        ScheduledJobListQuery query,
        CancellationToken cancellationToken = default);

    Task<ScheduledJobDto?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PageResult<ScheduledJobLogDto>> GetLogsAsync(
        Guid jobId,
        ScheduledJobLogListQuery query,
        CancellationToken cancellationToken = default);

    Task<PageResult<ScheduledJobLogDetailDto>> GetLogDetailsAsync(
        Guid logId,
        ScheduledJobLogDetailListQuery query,
        CancellationToken cancellationToken = default);

    Task<ScheduledJobDto?> UpdateAsync(
        Guid id,
        SaveScheduledJobRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScheduledJobDto>> GetDueJobsAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default);

    Task RecordExecutionAsync(
        Guid jobId,
        ScheduledJobExecutionRecord record,
        CancellationToken cancellationToken = default);
}

public interface IScheduledJobExecutor
{
    Task<ScheduledJobExecutionResult> ExecuteAsync(
        string jobKey,
        CancellationToken cancellationToken = default);
}

public interface IScheduledJobAppService
{
    Task<PageResult<ScheduledJobDto>> GetListAsync(
        ScheduledJobListQuery query,
        CancellationToken cancellationToken = default);

    Task<ScheduledJobDto?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PageResult<ScheduledJobLogDto>> GetLogsAsync(
        Guid jobId,
        ScheduledJobLogListQuery query,
        CancellationToken cancellationToken = default);

    Task<PageResult<ScheduledJobLogDetailDto>> GetLogDetailsAsync(
        Guid logId,
        ScheduledJobLogDetailListQuery query,
        CancellationToken cancellationToken = default);

    Task<ScheduledJobDto?> UpdateAsync(
        Guid id,
        SaveScheduledJobRequest request,
        CancellationToken cancellationToken = default);

    Task<ScheduledJobRunResultDto?> RunOnceAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<int> RunDueJobsAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default);
}
