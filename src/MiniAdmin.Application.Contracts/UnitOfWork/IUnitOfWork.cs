using MiniAdmin.Application.Contracts.Events;

namespace MiniAdmin.Application.Contracts.UnitOfWork;

public interface IUnitOfWork
{
    bool HasActiveTransaction { get; }

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);

    void AddPostCommitEvent(ILocalEvent @event);
}
