using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Events;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfOutboxMessageRepository(
    MiniAdminDbContext dbContext,
    DbContextOptions<MiniAdminDbContext> dbContextOptions) : IOutboxMessageRepository
{
    public async Task<PageResult<OutboxMessageDto>> GetListAsync(
        OutboxMessageListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var messages = dbContext.OutboxMessages.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            messages = messages.Where(x => x.Status == query.Status.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            messages = messages.Where(x => x.EventType.Contains(query.EventType.Trim()));
        }

        var total = await messages.CountAsync(cancellationToken);
        var items = await messages
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToDto(x))
            .ToArrayAsync(cancellationToken);
        return new PageResult<OutboxMessageDto>(items, total);
    }

    public async Task<IReadOnlyList<OutboxMessageLease>> AcquirePendingAsync(
        DateTimeOffset now,
        int limit,
        string leaseOwner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        var candidateIds = await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(x =>
                x.NextAttemptAt <= now &&
                ((x.Status == OutboxMessageStatuses.Pending || x.Status == OutboxMessageStatuses.Retry) ||
                 (x.Status == OutboxMessageStatuses.Processing && x.LeaseExpiresAt <= now)))
            .OrderBy(x => x.NextAttemptAt)
            .ThenBy(x => x.CreatedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);

        var leases = new List<OutboxMessageLease>(candidateIds.Length);
        foreach (var candidateId in candidateIds)
        {
            var lease = await TryAcquireAsync(
                candidateId,
                now,
                leaseOwner,
                leaseDuration,
                cancellationToken);
            if (lease is not null)
            {
                leases.Add(lease);
            }
        }

        return leases;
    }

    public async Task<bool> RenewLeaseAsync(
        Guid messageId,
        Guid leaseToken,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        await using var leaseDbContext = new MiniAdminDbContext(dbContextOptions);
        if (leaseDbContext.Database.IsRelational())
        {
            var affected = await leaseDbContext.OutboxMessages
                .Where(x =>
                    x.Id == messageId &&
                    x.LeaseToken == leaseToken &&
                    x.Status == OutboxMessageStatuses.Processing)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.LeaseExpiresAt, (DateTimeOffset?)now.Add(leaseDuration))
                        .SetProperty(x => x.UpdatedAt, now),
                    cancellationToken);
            return affected == 1;
        }

        var message = await leaseDbContext.OutboxMessages.SingleOrDefaultAsync(
            x => x.Id == messageId &&
                 x.LeaseToken == leaseToken &&
                 x.Status == OutboxMessageStatuses.Processing,
            cancellationToken);
        if (message is null)
        {
            return false;
        }

        message.LeaseExpiresAt = now.Add(leaseDuration);
        message.UpdatedAt = now;
        await leaseDbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> MarkSucceededAsync(
        Guid messageId,
        Guid leaseToken,
        DateTimeOffset processedAt,
        CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.IsRelational())
        {
            var affected = await dbContext.OutboxMessages
                .Where(x =>
                    x.Id == messageId &&
                    x.LeaseToken == leaseToken &&
                    x.Status == OutboxMessageStatuses.Processing)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.Status, OutboxMessageStatuses.Succeeded)
                        .SetProperty(x => x.ProcessedAt, (DateTimeOffset?)processedAt)
                        .SetProperty(x => x.LastError, (string?)null)
                        .SetProperty(x => x.LeaseToken, (Guid?)null)
                        .SetProperty(x => x.LeaseOwner, (string?)null)
                        .SetProperty(x => x.LeaseExpiresAt, (DateTimeOffset?)null)
                        .SetProperty(x => x.UpdatedAt, processedAt),
                    cancellationToken);
            return affected == 1;
        }

        var message = await FindLeasedAsync(messageId, leaseToken, cancellationToken);
        if (message is null)
        {
            return false;
        }

        message.Status = OutboxMessageStatuses.Succeeded;
        message.ProcessedAt = processedAt;
        message.LastError = null;
        ClearLease(message);
        message.UpdatedAt = processedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> MarkFailedAsync(
        Guid messageId,
        Guid leaseToken,
        string error,
        DateTimeOffset failedAt,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await dbContext.OutboxMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == messageId &&
                     x.LeaseToken == leaseToken &&
                     x.Status == OutboxMessageStatuses.Processing,
                cancellationToken);
        if (snapshot is null)
        {
            return false;
        }

        var attemptCount = snapshot.AttemptCount + 1;
        var deadLetter = attemptCount >= snapshot.MaxAttempts;
        var normalizedError = NormalizeError(error);
        var nextAttemptAt = deadLetter ? failedAt : failedAt.Add(retryDelay);

        if (dbContext.Database.IsRelational())
        {
            var affected = await dbContext.OutboxMessages
                .Where(x =>
                    x.Id == messageId &&
                    x.LeaseToken == leaseToken &&
                    x.Status == OutboxMessageStatuses.Processing)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            x => x.Status,
                            deadLetter ? OutboxMessageStatuses.DeadLetter : OutboxMessageStatuses.Retry)
                        .SetProperty(x => x.AttemptCount, attemptCount)
                        .SetProperty(x => x.NextAttemptAt, nextAttemptAt)
                        .SetProperty(x => x.LastError, normalizedError)
                        .SetProperty(x => x.LeaseToken, (Guid?)null)
                        .SetProperty(x => x.LeaseOwner, (string?)null)
                        .SetProperty(x => x.LeaseExpiresAt, (DateTimeOffset?)null)
                        .SetProperty(x => x.UpdatedAt, failedAt),
                    cancellationToken);
            return affected == 1;
        }

        var message = await FindLeasedAsync(messageId, leaseToken, cancellationToken);
        if (message is null)
        {
            return false;
        }

        message.Status = deadLetter ? OutboxMessageStatuses.DeadLetter : OutboxMessageStatuses.Retry;
        message.AttemptCount = attemptCount;
        message.NextAttemptAt = nextAttemptAt;
        message.LastError = normalizedError;
        ClearLease(message);
        message.UpdatedAt = failedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RetryAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (dbContext.Database.IsRelational())
        {
            var affected = await dbContext.OutboxMessages
                .Where(x =>
                    x.Id == messageId &&
                    (x.Status == OutboxMessageStatuses.DeadLetter || x.Status == OutboxMessageStatuses.Retry))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.Status, OutboxMessageStatuses.Pending)
                        .SetProperty(x => x.AttemptCount, 0)
                        .SetProperty(x => x.NextAttemptAt, now)
                        .SetProperty(x => x.LastError, (string?)null)
                        .SetProperty(x => x.LeaseToken, (Guid?)null)
                        .SetProperty(x => x.LeaseOwner, (string?)null)
                        .SetProperty(x => x.LeaseExpiresAt, (DateTimeOffset?)null)
                        .SetProperty(x => x.ProcessedAt, (DateTimeOffset?)null)
                        .SetProperty(x => x.UpdatedAt, now),
                    cancellationToken);
            return affected == 1;
        }

        var message = await dbContext.OutboxMessages.SingleOrDefaultAsync(
            x => x.Id == messageId &&
                 (x.Status == OutboxMessageStatuses.DeadLetter || x.Status == OutboxMessageStatuses.Retry),
            cancellationToken);
        if (message is null)
        {
            return false;
        }

        message.Status = OutboxMessageStatuses.Pending;
        message.AttemptCount = 0;
        message.NextAttemptAt = now;
        message.LastError = null;
        message.ProcessedAt = null;
        ClearLease(message);
        message.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<OutboxMessageLease?> TryAcquireAsync(
        Guid messageId,
        DateTimeOffset now,
        string leaseOwner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        var leaseToken = Guid.NewGuid();
        var leaseExpiresAt = now.Add(leaseDuration);
        if (dbContext.Database.IsRelational())
        {
            var affected = await dbContext.OutboxMessages
                .Where(x =>
                    x.Id == messageId &&
                    x.NextAttemptAt <= now &&
                    ((x.Status == OutboxMessageStatuses.Pending || x.Status == OutboxMessageStatuses.Retry) ||
                     (x.Status == OutboxMessageStatuses.Processing && x.LeaseExpiresAt <= now)))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.Status, OutboxMessageStatuses.Processing)
                        .SetProperty(x => x.LeaseToken, (Guid?)leaseToken)
                        .SetProperty(x => x.LeaseOwner, leaseOwner)
                        .SetProperty(x => x.LeaseExpiresAt, (DateTimeOffset?)leaseExpiresAt)
                        .SetProperty(x => x.UpdatedAt, now),
                    cancellationToken);
            if (affected != 1)
            {
                return null;
            }
        }
        else
        {
            var message = await dbContext.OutboxMessages.SingleOrDefaultAsync(x => x.Id == messageId, cancellationToken);
            if (message is null ||
                message.NextAttemptAt > now ||
                !IsAcquirable(message, now))
            {
                return null;
            }

            message.Status = OutboxMessageStatuses.Processing;
            message.LeaseToken = leaseToken;
            message.LeaseOwner = leaseOwner;
            message.LeaseExpiresAt = leaseExpiresAt;
            message.UpdatedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var leased = await dbContext.OutboxMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == messageId && x.LeaseToken == leaseToken,
                cancellationToken);
        return leased is null
            ? null
            : new OutboxMessageLease(
                leased.Id,
                leased.EventType,
                leased.Payload,
                leased.TenantId,
                leaseToken,
                leaseOwner,
                leaseExpiresAt,
                leased.AttemptCount,
                leased.MaxAttempts);
    }

    private Task<OutboxMessage?> FindLeasedAsync(
        Guid messageId,
        Guid leaseToken,
        CancellationToken cancellationToken)
    {
        return dbContext.OutboxMessages.SingleOrDefaultAsync(
            x => x.Id == messageId &&
                 x.LeaseToken == leaseToken &&
                 x.Status == OutboxMessageStatuses.Processing,
            cancellationToken);
    }

    private static bool IsAcquirable(OutboxMessage message, DateTimeOffset now)
    {
        return message.Status is OutboxMessageStatuses.Pending or OutboxMessageStatuses.Retry ||
               (message.Status == OutboxMessageStatuses.Processing && message.LeaseExpiresAt <= now);
    }

    private static void ClearLease(OutboxMessage message)
    {
        message.LeaseToken = null;
        message.LeaseOwner = null;
        message.LeaseExpiresAt = null;
    }

    private static string NormalizeError(string error)
    {
        var normalized = string.IsNullOrWhiteSpace(error) ? "Unknown outbox delivery error." : error.Trim();
        return normalized.Length <= 4000 ? normalized : normalized[..4000];
    }

    private static OutboxMessageDto ToDto(OutboxMessage message)
    {
        return new OutboxMessageDto(
            message.Id.ToString(),
            message.EventType,
            message.Status,
            message.AttemptCount,
            message.MaxAttempts,
            message.OccurredAt,
            message.NextAttemptAt,
            message.TenantId?.ToString(),
            message.CorrelationId,
            message.LeaseOwner,
            message.LeaseExpiresAt,
            message.ProcessedAt,
            message.LastError,
            message.CreatedAt,
            message.UpdatedAt);
    }
}
