using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniAdmin.Application.Contracts.Events;
using MiniAdmin.Application.Contracts.UnitOfWork;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Infrastructure.Events;
using MiniAdmin.Infrastructure.Persistence;
using MiniAdmin.Infrastructure.UnitOfWork;

namespace MiniAdmin.Tests;

public sealed class PlatformInfrastructureTests
{
    [Theory]
    [InlineData(typeof(Role))]
    [InlineData(typeof(Department))]
    [InlineData(typeof(Position))]
    public void TenantScopedCoreEntitiesUseTenantScopedCodeUniqueIndex(Type entityType)
    {
        var options = new DbContextOptionsBuilder<MiniAdminDbContext>()
            .UseInMemoryDatabase($"tenant-scoped-index-{Guid.NewGuid():N}")
            .Options;
        using var dbContext = new MiniAdminDbContext(options);
        var entity = dbContext.Model.FindEntityType(entityType);

        Assert.NotNull(entity);
        Assert.DoesNotContain(
            entity.GetIndexes(),
            index => index.IsUnique &&
                     index.Properties.Select(property => property.Name).SequenceEqual(["Code"]));
        Assert.Contains(
            entity.GetIndexes(),
            index => index.IsUnique &&
                     index.Properties.Select(property => property.Name).SequenceEqual(["TenantId", "Code"]));
    }

    [Fact]
    public async Task LocalEventBusPublishesEventToHandlersInRegistrationOrder()
    {
        var services = new ServiceCollection();
        services.AddScoped<EventRecorder>();
        services.AddScoped<ILocalEventBus, LocalEventBus>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILocalEventHandler<TestLocalEvent>, FirstTestLocalEventHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILocalEventHandler<TestLocalEvent>, SecondTestLocalEventHandler>());

        await using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var eventBus = scope.ServiceProvider.GetRequiredService<ILocalEventBus>();
        await eventBus.PublishAsync(new TestLocalEvent("created"));

        var recorder = scope.ServiceProvider.GetRequiredService<EventRecorder>();
        Assert.Equal(["first:created", "second:created"], recorder.Items);
    }

    [Fact]
    public async Task LocalEventBusPropagatesHandlerException()
    {
        var services = new ServiceCollection();
        services.AddScoped<EventRecorder>();
        services.AddScoped<ILocalEventBus, LocalEventBus>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILocalEventHandler<TestLocalEvent>, FailingTestLocalEventHandler>());

        await using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var eventBus = scope.ServiceProvider.GetRequiredService<ILocalEventBus>();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await eventBus.PublishAsync(new TestLocalEvent("failed")));

        Assert.Equal("handler failed: failed", exception.Message);
    }

    [Fact]
    public async Task UnitOfWorkDispatchesPostCommitEventsAfterSaveChangesWithoutExplicitTransaction()
    {
        var services = CreateUnitOfWorkServices();

        await using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var recorder = scope.ServiceProvider.GetRequiredService<EventRecorder>();

        unitOfWork.AddPostCommitEvent(new TestLocalEvent("saved"));
        Assert.Empty(recorder.Items);

        await unitOfWork.SaveChangesAsync();

        Assert.Equal(["first:saved"], recorder.Items);
    }

    [Fact]
    public async Task UnitOfWorkDispatchesPostCommitEventsAfterExplicitCommit()
    {
        var services = CreateUnitOfWorkServices();

        await using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var recorder = scope.ServiceProvider.GetRequiredService<EventRecorder>();

        await unitOfWork.BeginTransactionAsync();
        unitOfWork.AddPostCommitEvent(new TestLocalEvent("committed"));
        await unitOfWork.SaveChangesAsync();

        Assert.Empty(recorder.Items);

        await unitOfWork.CommitAsync();

        Assert.Equal(["first:committed"], recorder.Items);
    }

    [Fact]
    public async Task UnitOfWorkClearsPostCommitEventsAfterRollback()
    {
        var services = CreateUnitOfWorkServices();

        await using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var recorder = scope.ServiceProvider.GetRequiredService<EventRecorder>();

        await unitOfWork.BeginTransactionAsync();
        unitOfWork.AddPostCommitEvent(new TestLocalEvent("rolled-back"));
        await unitOfWork.RollbackAsync();

        Assert.Empty(recorder.Items);
    }

    [Fact]
    public async Task UnitOfWorkPropagatesPostCommitEventHandlerException()
    {
        var services = CreateUnitOfWorkServices();
        services.RemoveAll<ILocalEventHandler<TestLocalEvent>>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILocalEventHandler<TestLocalEvent>, FailingTestLocalEventHandler>());

        await using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        unitOfWork.AddPostCommitEvent(new TestLocalEvent("failed-after-save"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await unitOfWork.SaveChangesAsync());

        Assert.Equal("handler failed: failed-after-save", exception.Message);
    }

    private static ServiceCollection CreateUnitOfWorkServices()
    {
        var services = new ServiceCollection();
        services.AddDbContext<MiniAdminDbContext>(options =>
            options.UseInMemoryDatabase($"unit-of-work-{Guid.NewGuid():N}"));
        services.AddScoped<EventRecorder>();
        services.AddScoped<ILocalEventBus, LocalEventBus>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILocalEventHandler<TestLocalEvent>, FirstTestLocalEventHandler>());
        return services;
    }

    private sealed record TestLocalEvent(string Name) : ILocalEvent;

    private sealed class EventRecorder
    {
        public List<string> Items { get; } = [];
    }

    private sealed class FirstTestLocalEventHandler(EventRecorder recorder) : ILocalEventHandler<TestLocalEvent>
    {
        public Task HandleAsync(TestLocalEvent @event, CancellationToken cancellationToken = default)
        {
            recorder.Items.Add($"first:{@event.Name}");
            return Task.CompletedTask;
        }
    }

    private sealed class SecondTestLocalEventHandler(EventRecorder recorder) : ILocalEventHandler<TestLocalEvent>
    {
        public Task HandleAsync(TestLocalEvent @event, CancellationToken cancellationToken = default)
        {
            recorder.Items.Add($"second:{@event.Name}");
            return Task.CompletedTask;
        }
    }

    private sealed class FailingTestLocalEventHandler : ILocalEventHandler<TestLocalEvent>
    {
        public Task HandleAsync(TestLocalEvent @event, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException($"handler failed: {@event.Name}");
        }
    }
}
