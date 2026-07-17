using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.Events;

public static class OutboxMessageStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Retry = "Retry";
    public const string Succeeded = "Succeeded";
    public const string DeadLetter = "DeadLetter";
}

public sealed record OutboxMessageListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? EventType = null);

public sealed record OutboxMessageDto(
    string Id,
    string EventType,
    string Status,
    int AttemptCount,
    int MaxAttempts,
    DateTimeOffset OccurredAt,
    DateTimeOffset NextAttemptAt,
    string? TenantId,
    string? CorrelationId,
    string? LeaseOwner,
    DateTimeOffset? LeaseExpiresAt,
    DateTimeOffset? ProcessedAt,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record OutboxMessageLease(
    Guid Id,
    string EventType,
    string Payload,
    Guid? TenantId,
    Guid LeaseToken,
    string LeaseOwner,
    DateTimeOffset LeaseExpiresAt,
    int AttemptCount,
    int MaxAttempts);

public interface IOutboxExecutionContext
{
    string WorkerId { get; }

    TimeSpan LeaseDuration { get; }

    TimeSpan HeartbeatInterval { get; }

    TimeSpan PollInterval { get; }

    TimeSpan RetryBaseDelay { get; }

    TimeSpan RetryMaxDelay { get; }

    int BatchSize { get; }

    int DefaultMaxAttempts { get; }
}

public interface IOutboxMessageRepository
{
    Task<PageResult<OutboxMessageDto>> GetListAsync(
        OutboxMessageListQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutboxMessageLease>> AcquirePendingAsync(
        DateTimeOffset now,
        int limit,
        string leaseOwner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task<bool> RenewLeaseAsync(
        Guid messageId,
        Guid leaseToken,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task<bool> MarkSucceededAsync(
        Guid messageId,
        Guid leaseToken,
        DateTimeOffset processedAt,
        CancellationToken cancellationToken = default);

    Task<bool> MarkFailedAsync(
        Guid messageId,
        Guid leaseToken,
        string error,
        DateTimeOffset failedAt,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default);

    Task<bool> RetryAsync(Guid messageId, CancellationToken cancellationToken = default);
}

public interface IOutboxAppService
{
    Task<PageResult<OutboxMessageDto>> GetListAsync(
        OutboxMessageListQuery query,
        CancellationToken cancellationToken = default);

    Task<bool> RetryAsync(Guid messageId, CancellationToken cancellationToken = default);
}
