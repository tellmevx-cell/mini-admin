using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Events;

namespace MiniAdmin.Application.Events;

public sealed class OutboxAppService(IOutboxMessageRepository repository) : IOutboxAppService
{
    public Task<PageResult<OutboxMessageDto>> GetListAsync(
        OutboxMessageListQuery query,
        CancellationToken cancellationToken = default)
    {
        return repository.GetListAsync(query, cancellationToken);
    }

    public Task<bool> RetryAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return repository.RetryAsync(messageId, cancellationToken);
    }
}
