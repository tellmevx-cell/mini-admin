using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using MiniAdmin.Application.Contracts.Events;

namespace MiniAdmin.Infrastructure.Events;

public sealed class LocalEventBus(IServiceProvider serviceProvider) : ILocalEventBus
{
    public async Task PublishAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : ILocalEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        var handlers = serviceProvider.GetServices<ILocalEventHandler<TEvent>>();
        foreach (var handler in handlers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await handler.HandleAsync(@event, cancellationToken);
        }
    }

    public async Task PublishAsync(
        ILocalEvent @event,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var handlerType = typeof(ILocalEventHandler<>).MakeGenericType(@event.GetType());
        var handlers = serviceProvider.GetServices(handlerType);
        var handleMethod = handlerType.GetMethod(nameof(ILocalEventHandler<ILocalEvent>.HandleAsync))
            ?? throw new InvalidOperationException($"Cannot find HandleAsync method for {@event.GetType().Name}.");

        foreach (var handler in handlers.OfType<object>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            Task? task;
            try
            {
                task = (Task?)handleMethod.Invoke(handler, [@event, cancellationToken]);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }

            if (task is null)
            {
                throw new InvalidOperationException(
                    $"Local event handler {handler.GetType().FullName} returned null task.");
            }

            await task;
        }
    }
}
