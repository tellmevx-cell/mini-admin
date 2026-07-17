using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniAdmin.Application.Contracts.Events;
using MiniAdmin.Infrastructure.MultiTenancy;

namespace MiniAdmin.Infrastructure.Events;

public sealed class OutboxWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOutboxExecutionContext executionContext,
    ILogger<OutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            IReadOnlyList<OutboxMessageLease> leases;
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
                leases = await repository.AcquirePendingAsync(
                    DateTimeOffset.UtcNow,
                    executionContext.BatchSize,
                    executionContext.WorkerId,
                    executionContext.LeaseDuration,
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox worker failed to acquire messages.");
                await DelayAsync(stoppingToken);
                continue;
            }

            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            // Every acquired lease starts its heartbeat immediately; queued leases must not expire
            // while an earlier message is still being handled.
            await Task.WhenAll(leases.Select(lease => ProcessAsync(lease, stoppingToken)));

            if (leases.Count < executionContext.BatchSize)
            {
                await DelayAsync(stoppingToken);
            }
        }
    }

    private async Task ProcessAsync(OutboxMessageLease lease, CancellationToken stoppingToken)
    {
        var leaseLost = false;
        using var processingCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var heartbeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var heartbeatTask = MaintainLeaseAsync(
            lease,
            processingCancellation,
            heartbeatCancellation.Token,
            () => leaseLost = true);

        Exception? deliveryException = null;
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var currentTenant = scope.ServiceProvider.GetRequiredService<CurrentTenant>();
            currentTenant.Change(lease.TenantId, null);
            var serializer = scope.ServiceProvider.GetRequiredService<IOutboxEventSerializer>();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxEventDispatcher>();
            var @event = serializer.Deserialize(lease.EventType, lease.Payload);
            await dispatcher.DispatchAsync(lease.Id, @event, processingCancellation.Token);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            heartbeatCancellation.Cancel();
            await AwaitHeartbeatAsync(heartbeatTask);
            return;
        }
        catch (Exception exception)
        {
            deliveryException = exception;
        }
        finally
        {
            heartbeatCancellation.Cancel();
        }

        await AwaitHeartbeatAsync(heartbeatTask);
        if (leaseLost)
        {
            logger.LogWarning(
                "Outbox message {MessageId} lease was lost; current worker will not update its status.",
                lease.Id);
            return;
        }

        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
            if (deliveryException is null)
            {
                var completed = await repository.MarkSucceededAsync(
                    lease.Id,
                    lease.LeaseToken,
                    DateTimeOffset.UtcNow,
                    stoppingToken);
                if (!completed)
                {
                    logger.LogWarning("Outbox message {MessageId} completion lost its lease.", lease.Id);
                }

                return;
            }

            var retryDelay = CalculateRetryDelay(lease.AttemptCount);
            var recorded = await repository.MarkFailedAsync(
                lease.Id,
                lease.LeaseToken,
                deliveryException.ToString(),
                DateTimeOffset.UtcNow,
                retryDelay,
                stoppingToken);
            if (recorded)
            {
                logger.LogWarning(
                    deliveryException,
                    "Outbox message {MessageId} delivery failed. Attempt {Attempt}/{MaxAttempts}; retry delay {RetryDelay}.",
                    lease.Id,
                    lease.AttemptCount + 1,
                    lease.MaxAttempts,
                    retryDelay);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The finite lease allows another instance to recover this message.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Outbox message {MessageId} status update failed.", lease.Id);
        }
    }

    private async Task MaintainLeaseAsync(
        OutboxMessageLease lease,
        CancellationTokenSource processingCancellation,
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
                    using var scope = serviceScopeFactory.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
                    var now = DateTimeOffset.UtcNow;
                    var renewed = await repository.RenewLeaseAsync(
                        lease.Id,
                        lease.LeaseToken,
                        now,
                        executionContext.LeaseDuration,
                        cancellationToken);
                    if (!renewed)
                    {
                        onLeaseLost();
                        processingCancellation.Cancel();
                        return;
                    }

                    validUntil = now.Add(executionContext.LeaseDuration);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Outbox message {MessageId} heartbeat failed.", lease.Id);
                    if (DateTimeOffset.UtcNow < validUntil)
                    {
                        continue;
                    }

                    onLeaseLost();
                    processingCancellation.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal processing completion or host shutdown.
        }
    }

    private TimeSpan CalculateRetryDelay(int previousAttemptCount)
    {
        var exponent = Math.Min(previousAttemptCount, 20);
        var multiplier = Math.Pow(2, exponent);
        var seconds = Math.Min(
            executionContext.RetryMaxDelay.TotalSeconds,
            executionContext.RetryBaseDelay.TotalSeconds * multiplier);
        return TimeSpan.FromSeconds(seconds);
    }

    private async Task DelayAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(executionContext.PollInterval, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown.
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
            // Expected when processing completes.
        }
    }
}
