using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniAdmin.Application.Contracts.Events;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Infrastructure.Events;

public interface IOutboxEventDispatcher
{
    Task DispatchAsync(
        Guid messageId,
        IOutboxEvent @event,
        CancellationToken cancellationToken = default);
}

public sealed class OutboxEventDispatcher(
    IServiceProvider serviceProvider,
    MiniAdminDbContext dbContext) : IOutboxEventDispatcher
{
    public async Task DispatchAsync(
        Guid messageId,
        IOutboxEvent @event,
        CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(ILocalEventHandler<>).MakeGenericType(@event.GetType());
        var handlers = serviceProvider.GetServices(handlerType).OfType<object>().ToArray();
        if (handlers.Length == 0)
        {
            throw new InvalidOperationException(
                $"No local event handler is registered for durable event {@event.GetType().FullName}.");
        }

        var handleMethod = handlerType.GetMethod(nameof(ILocalEventHandler<ILocalEvent>.HandleAsync))
            ?? throw new InvalidOperationException($"Cannot find HandleAsync for {@event.GetType().FullName}.");

        foreach (var handler in handlers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var consumerName = GetConsumerName(handler.GetType());
            if (await dbContext.InboxMessages
                    .AsNoTracking()
                    .AnyAsync(
                        x => x.MessageId == messageId && x.ConsumerName == consumerName,
                        cancellationToken))
            {
                continue;
            }

            await DispatchToHandlerAsync(
                messageId,
                @event,
                handler,
                consumerName,
                handleMethod,
                cancellationToken);
        }
    }

    private async Task DispatchToHandlerAsync(
        Guid messageId,
        IOutboxEvent @event,
        object handler,
        string consumerName,
        MethodInfo handleMethod,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            await InvokeHandlerAsync(handler, handleMethod, @event, cancellationToken);
            dbContext.InboxMessages.Add(CreateInboxMessage(messageId, consumerName));
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Handler database writes and the Inbox receipt share this transaction.
            await InvokeHandlerAsync(handler, handleMethod, @event, cancellationToken);
            dbContext.InboxMessages.Add(CreateInboxMessage(messageId, consumerName));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task InvokeHandlerAsync(
        object handler,
        MethodInfo handleMethod,
        IOutboxEvent @event,
        CancellationToken cancellationToken)
    {
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
                $"Outbox event handler {handler.GetType().FullName} returned a null task.");
        }

        await task;
    }

    private static InboxMessage CreateInboxMessage(Guid messageId, string consumerName)
    {
        return new InboxMessage
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            ConsumerName = consumerName,
            ProcessedAt = DateTimeOffset.UtcNow
        };
    }

    private static string GetConsumerName(Type handlerType)
    {
        var name = handlerType.FullName ?? handlerType.Name;
        return name.Length <= 512 ? name : name[..512];
    }
}
