using MiniAdmin.Application.Contracts.Chat;
using MiniAdmin.Application.Contracts.Security;
using MiniAdmin.Platform.DynamicApi;

namespace MiniAdmin.Application.Chat;

[DynamicApi("message/chat", Name = "Chat", Tag = "在线聊天")]
public sealed class ChatAppService(
    IChatRepository repository,
    ICurrentUserContext currentUser) : IChatAppService
{
    [DynamicGet(
        "contacts",
        Permission = "message:chat:query",
        Resource = "message.chat",
        Action = "query",
        OperationId = "GetChatContacts",
        Summary = "查询当前租户可聊天联系人")]
    public Task<IReadOnlyList<ChatContactDto>> GetContactsAsync(
        string? keyword = null,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        return repository.GetContactsAsync(
            currentUser.UserId,
            currentUser.TenantId,
            keyword,
            Math.Clamp(take, 1, 100),
            cancellationToken);
    }

    [DynamicGet(
        "conversations",
        Permission = "message:chat:query",
        Resource = "message.chat",
        Action = "query",
        OperationId = "GetChatConversations",
        Summary = "查询我的聊天会话")]
    public Task<IReadOnlyList<ChatConversationDto>> GetConversationsAsync(
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        return repository.GetConversationsAsync(
            currentUser.UserId,
            currentUser.TenantId,
            Math.Clamp(take, 1, 100),
            cancellationToken);
    }

    [DynamicGet(
        "conversations/{conversationId:guid}/messages",
        Permission = "message:chat:query",
        Resource = "message.chat",
        Action = "query",
        OperationId = "GetChatMessages",
        Summary = "查询会话历史消息")]
    public Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(
        [DynamicApiParameter(DynamicApiParameterSource.Route)] Guid conversationId,
        ChatMessageListQuery query,
        CancellationToken cancellationToken = default)
    {
        return repository.GetMessagesAsync(
            currentUser.UserId,
            currentUser.TenantId,
            conversationId,
            query with { Take = Math.Clamp(query.Take, 1, 100) },
            cancellationToken);
    }

    [DynamicPost(
        "messages",
        Permission = "message:chat:send",
        Resource = "message.chat",
        Action = "send",
        OperationId = "SendChatMessage",
        Summary = "发送聊天消息")]
    public Task<ChatMessageDto> SendAsync(
        SendChatMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        return repository.SendAsync(
            currentUser.UserId,
            currentUser.TenantId,
            request,
            DateTimeOffset.UtcNow,
            cancellationToken);
    }

    [DynamicPost(
        "conversations/{conversationId:guid}/read",
        Permission = "message:chat:read",
        Resource = "message.chat",
        Action = "read",
        OperationId = "MarkChatConversationRead",
        Summary = "标记会话消息为已读")]
    public Task<ChatReadReceiptDto> MarkReadAsync(
        [DynamicApiParameter(DynamicApiParameterSource.Route)] Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        return repository.MarkReadAsync(
            currentUser.UserId,
            currentUser.TenantId,
            conversationId,
            DateTimeOffset.UtcNow,
            cancellationToken);
    }
}
