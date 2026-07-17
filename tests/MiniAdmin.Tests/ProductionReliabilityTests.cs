using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniAdmin.Application.Contracts.Events;
using MiniAdmin.Application.Contracts.ScheduledJobs;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Infrastructure.Events;
using MiniAdmin.Infrastructure.MultiTenancy;
using MiniAdmin.Infrastructure.Persistence;
using MiniAdmin.Infrastructure.UnitOfWork;

namespace MiniAdmin.Tests;

public sealed class ProductionReliabilityTests
{
    [Fact]
    public async Task ScheduledJobLeasePreventsDuplicateExecutionAndAllowsExpiredTakeover()
    {
        var options = CreateOptions("scheduled-job-lease");
        var jobId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using (var seedContext = new MiniAdminDbContext(options))
        {
            seedContext.ScheduledJobs.Add(new ScheduledJob
            {
                Id = jobId,
                JobKey = "lease-test",
                Name = "Lease test",
                IntervalSeconds = 60,
                IsEnabled = true,
                NextRunAt = now.AddSeconds(-1)
            });
            await seedContext.SaveChangesAsync();
        }

        ScheduledJobLease firstLease;
        await using (var firstContext = new MiniAdminDbContext(options))
        {
            var firstRepository = new EfScheduledJobRepository(firstContext, options);
            firstLease = Assert.Single(await firstRepository.AcquireDueJobsAsync(
                now,
                1,
                "worker-one",
                TimeSpan.FromMinutes(2)));
        }

        await using (var competingContext = new MiniAdminDbContext(options))
        {
            var competingRepository = new EfScheduledJobRepository(competingContext, options);
            Assert.Empty(await competingRepository.AcquireDueJobsAsync(
                now.AddSeconds(30),
                1,
                "worker-two",
                TimeSpan.FromMinutes(2)));
        }

        ScheduledJobLease takeoverLease;
        await using (var takeoverContext = new MiniAdminDbContext(options))
        {
            var takeoverRepository = new EfScheduledJobRepository(takeoverContext, options);
            takeoverLease = Assert.Single(await takeoverRepository.AcquireDueJobsAsync(
                now.AddMinutes(3),
                1,
                "worker-two",
                TimeSpan.FromMinutes(2)));

            var staleRecorded = await takeoverRepository.RecordExecutionAsync(
                jobId,
                firstLease.LeaseToken,
                ExecutionRecord(now));
            Assert.False(staleRecorded);

            var takeoverRecorded = await takeoverRepository.RecordExecutionAsync(
                jobId,
                takeoverLease.LeaseToken,
                ExecutionRecord(now.AddMinutes(3)));
            Assert.True(takeoverRecorded);
        }

        await using var assertionContext = new MiniAdminDbContext(options);
        Assert.Single(assertionContext.ScheduledJobLogs);
        var job = await assertionContext.ScheduledJobs.SingleAsync(x => x.Id == jobId);
        Assert.Null(job.LeaseToken);
        Assert.Equal("Succeeded", job.LastStatus);
    }

    [Fact]
    public async Task UnitOfWorkPersistsOutboxEventInsteadOfDispatchingItInline()
    {
        var options = CreateOptions("unit-of-work-outbox");
        await using var dbContext = new MiniAdminDbContext(options);
        var localBus = new RecordingLocalEventBus();
        var tenantId = Guid.NewGuid();
        var currentTenant = new CurrentTenant();
        currentTenant.Change(tenantId, "tenant-a");
        var unitOfWork = new EfUnitOfWork(
            dbContext,
            localBus,
            new OutboxEventSerializer(),
            new TestOutboxExecutionContext(),
            currentTenant);

        unitOfWork.AddOutboxEvent(new ReliabilityTestEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "saved"));
        await unitOfWork.SaveChangesAsync();

        Assert.Equal(0, localBus.PublishCount);
        var message = await dbContext.OutboxMessages.SingleAsync();
        Assert.Equal(OutboxMessageStatuses.Pending, message.Status);
        Assert.Equal(tenantId, message.TenantId);
        Assert.Contains(nameof(ReliabilityTestEvent), message.EventType);
        Assert.Contains("saved", message.Payload);

        var repository = new EfOutboxMessageRepository(dbContext, options);
        var lease = Assert.Single(await repository.AcquirePendingAsync(
            DateTimeOffset.UtcNow,
            1,
            "tenant-worker",
            TimeSpan.FromMinutes(1)));
        Assert.Equal(tenantId, lease.TenantId);
    }

    [Fact]
    public async Task OutboxRepositoryRetriesThenDeadLettersAndSupportsManualRetry()
    {
        var options = CreateOptions("outbox-retry");
        var messageId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using (var seedContext = new MiniAdminDbContext(options))
        {
            seedContext.OutboxMessages.Add(new OutboxMessage
            {
                Id = messageId,
                EventType = "test",
                Payload = "{}",
                Status = OutboxMessageStatuses.Pending,
                MaxAttempts = 2,
                OccurredAt = now,
                NextAttemptAt = now,
                CreatedAt = now,
                UpdatedAt = now
            });
            await seedContext.SaveChangesAsync();
        }

        await using (var firstContext = new MiniAdminDbContext(options))
        {
            var repository = new EfOutboxMessageRepository(firstContext, options);
            var lease = Assert.Single(await repository.AcquirePendingAsync(
                now,
                1,
                "worker-one",
                TimeSpan.FromMinutes(1)));
            Assert.True(await repository.MarkFailedAsync(
                messageId,
                lease.LeaseToken,
                "temporary",
                now,
                TimeSpan.FromSeconds(5)));
        }

        await using (var secondContext = new MiniAdminDbContext(options))
        {
            var repository = new EfOutboxMessageRepository(secondContext, options);
            var lease = Assert.Single(await repository.AcquirePendingAsync(
                now.AddSeconds(6),
                1,
                "worker-two",
                TimeSpan.FromMinutes(1)));
            Assert.True(await repository.MarkFailedAsync(
                messageId,
                lease.LeaseToken,
                "permanent",
                now.AddSeconds(6),
                TimeSpan.FromSeconds(10)));

            var deadLetter = await secondContext.OutboxMessages.AsNoTracking().SingleAsync();
            Assert.Equal(OutboxMessageStatuses.DeadLetter, deadLetter.Status);
            Assert.Equal(2, deadLetter.AttemptCount);

            Assert.True(await repository.RetryAsync(messageId));
            var retried = await secondContext.OutboxMessages.AsNoTracking().SingleAsync();
            Assert.Equal(OutboxMessageStatuses.Pending, retried.Status);
            Assert.Equal(0, retried.AttemptCount);
            Assert.Null(retried.LastError);
        }
    }

    [Fact]
    public async Task OutboxDispatcherUsesInboxToSkipAnAlreadyProcessedConsumer()
    {
        var services = new ServiceCollection();
        services.AddDbContext<MiniAdminDbContext>(options =>
            options.UseInMemoryDatabase($"outbox-inbox-{Guid.NewGuid():N}"));
        services.AddScoped<IOutboxEventDispatcher, OutboxEventDispatcher>();
        services.AddScoped<ILocalEventHandler<ReliabilityTestEvent>, ReliabilityTestEventHandler>();
        services.AddSingleton<ReliabilityEventRecorder>();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxEventDispatcher>();
        var messageId = Guid.NewGuid();

        var @event = new ReliabilityTestEvent(messageId, DateTimeOffset.UtcNow, "once");
        await dispatcher.DispatchAsync(messageId, @event);
        await dispatcher.DispatchAsync(messageId, @event);

        var recorder = scope.ServiceProvider.GetRequiredService<ReliabilityEventRecorder>();
        Assert.Equal(1, recorder.Count);
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        Assert.Single(dbContext.InboxMessages);
    }

    private static DbContextOptions<MiniAdminDbContext> CreateOptions(string prefix)
    {
        return new DbContextOptionsBuilder<MiniAdminDbContext>()
            .UseInMemoryDatabase($"{prefix}-{Guid.NewGuid():N}")
            .Options;
    }

    private static ScheduledJobExecutionRecord ExecutionRecord(DateTimeOffset startedAt)
    {
        return new ScheduledJobExecutionRecord(
            "Auto",
            "Succeeded",
            "ok",
            startedAt,
            startedAt.AddSeconds(1),
            1000);
    }

    public sealed record ReliabilityTestEvent(
        Guid EventId,
        DateTimeOffset OccurredAt,
        string Value) : IOutboxEvent;

    public sealed class ReliabilityTestEventHandler(ReliabilityEventRecorder recorder)
        : ILocalEventHandler<ReliabilityTestEvent>
    {
        public Task HandleAsync(
            ReliabilityTestEvent @event,
            CancellationToken cancellationToken = default)
        {
            recorder.Count++;
            return Task.CompletedTask;
        }
    }

    public sealed class ReliabilityEventRecorder
    {
        public int Count { get; set; }
    }

    private sealed class RecordingLocalEventBus : ILocalEventBus
    {
        public int PublishCount { get; private set; }

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : ILocalEvent
        {
            PublishCount++;
            return Task.CompletedTask;
        }

        public Task PublishAsync(ILocalEvent @event, CancellationToken cancellationToken = default)
        {
            PublishCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class TestOutboxExecutionContext : IOutboxExecutionContext
    {
        public string WorkerId => "test";

        public TimeSpan LeaseDuration => TimeSpan.FromMinutes(1);

        public TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(10);

        public TimeSpan PollInterval => TimeSpan.FromSeconds(1);

        public TimeSpan RetryBaseDelay => TimeSpan.FromSeconds(1);

        public TimeSpan RetryMaxDelay => TimeSpan.FromMinutes(1);

        public int BatchSize => 10;

        public int DefaultMaxAttempts => 3;
    }
}
