namespace MiniAdmin.Application.Contracts.Events;

public interface ILocalEvent;

/// <summary>
/// Marks an event that must be persisted with the current business transaction and
/// delivered with at-least-once semantics.
/// </summary>
public interface IOutboxEvent : ILocalEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }
}

public interface ILocalEventHandler<in TEvent>
    where TEvent : ILocalEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}

public interface ILocalEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : ILocalEvent;

    Task PublishAsync(ILocalEvent @event, CancellationToken cancellationToken = default);
}
