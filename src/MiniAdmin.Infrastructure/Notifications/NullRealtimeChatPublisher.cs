using MiniAdmin.Application.Contracts.Chat;

namespace MiniAdmin.Infrastructure.Notifications;

public sealed class NullRealtimeChatPublisher : IRealtimeChatPublisher
{
    public Task PublishMessageAsync(
        ChatMessageDto message,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task PublishReadReceiptAsync(
        Guid recipientUserId,
        ChatReadReceiptDto receipt,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
