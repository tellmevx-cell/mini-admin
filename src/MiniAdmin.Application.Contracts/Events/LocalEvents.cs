namespace MiniAdmin.Application.Contracts.Events;

public interface ILocalEvent;

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
