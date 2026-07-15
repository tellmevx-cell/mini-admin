using Microsoft.AspNetCore.SignalR;
using MiniAdmin.Application.Contracts.Chat;

namespace MiniAdmin.Api.Hubs;

public sealed class SignalRRealtimeChatPublisher(
    IHubContext<ChatHub, IChatHubClient> hubContext,
    ILogger<SignalRRealtimeChatPublisher> logger) : IRealtimeChatPublisher
{
    public async Task PublishMessageAsync(
        ChatMessageDto message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await hubContext.Clients.Users(
                message.SenderId.ToString(),
                message.ReceiverId.ToString()).MessageReceived(message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "推送聊天消息 {MessageId} 失败。", message.Id);
        }
    }

    public async Task PublishReadReceiptAsync(
        Guid recipientUserId,
        ChatReadReceiptDto receipt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await hubContext.Clients.User(recipientUserId.ToString()).MessagesRead(receipt);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "推送会话 {ConversationId} 已读回执失败。", receipt.ConversationId);
        }
    }
}
