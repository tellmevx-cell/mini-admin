using Microsoft.EntityFrameworkCore.Storage;
using MiniAdmin.Application.Contracts.Events;
using MiniAdmin.Application.Contracts.UnitOfWork;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Infrastructure.UnitOfWork;

public sealed class EfUnitOfWork(
    MiniAdminDbContext dbContext,
    ILocalEventBus localEventBus) : IUnitOfWork, IDisposable, IAsyncDisposable
{
    private readonly List<ILocalEvent> postCommitEvents = [];
    private IDbContextTransaction? currentTransaction;
    private bool noOpTransactionActive;

    public bool HasActiveTransaction => currentTransaction is not null || noOpTransactionActive;

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (HasActiveTransaction)
        {
            throw new InvalidOperationException("A unit of work transaction is already active.");
        }

        try
        {
            currentTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        }
        catch (InvalidOperationException exception) when (IsTransactionNotSupported(exception))
        {
            // EF InMemory does not support real transactions; keep the API usable for tests and local demos.
            noOpTransactionActive = true;
        }
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = await dbContext.SaveChangesAsync(cancellationToken);
        if (!HasActiveTransaction)
        {
            await DispatchPostCommitEventsAsync(cancellationToken);
        }

        return result;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();

        await dbContext.SaveChangesAsync(cancellationToken);
        if (currentTransaction is not null)
        {
            await currentTransaction.CommitAsync(cancellationToken);
            await currentTransaction.DisposeAsync();
            currentTransaction = null;
        }

        noOpTransactionActive = false;
        await DispatchPostCommitEventsAsync(cancellationToken);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();

        postCommitEvents.Clear();
        if (currentTransaction is not null)
        {
            await currentTransaction.RollbackAsync(cancellationToken);
            await currentTransaction.DisposeAsync();
            currentTransaction = null;
        }

        noOpTransactionActive = false;
    }

    public void AddPostCommitEvent(ILocalEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        postCommitEvents.Add(@event);
    }

    public async ValueTask DisposeAsync()
    {
        if (currentTransaction is not null)
        {
            await currentTransaction.DisposeAsync();
            currentTransaction = null;
        }
    }

    public void Dispose()
    {
        currentTransaction?.Dispose();
        currentTransaction = null;
    }

    private static bool IsTransactionNotSupported(InvalidOperationException exception)
    {
        return exception.Message.Contains("Transactions are not supported", StringComparison.OrdinalIgnoreCase) ||
               exception.Message.Contains("TransactionIgnoredWarning", StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureActiveTransaction()
    {
        if (!HasActiveTransaction)
        {
            throw new InvalidOperationException("No active unit of work transaction.");
        }
    }

    private async Task DispatchPostCommitEventsAsync(CancellationToken cancellationToken)
    {
        if (postCommitEvents.Count == 0)
        {
            return;
        }

        var events = postCommitEvents.ToArray();
        postCommitEvents.Clear();

        foreach (var @event in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await localEventBus.PublishAsync(@event, cancellationToken);
        }
    }
}
