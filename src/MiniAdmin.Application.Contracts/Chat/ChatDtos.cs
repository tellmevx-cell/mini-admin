using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.Chat;

public sealed record ChatContactDto(
    Guid Id,
    string UserName,
    string RealName);

public sealed record ChatMessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string SenderName,
    Guid ReceiverId,
    string ReceiverName,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public sealed record ChatConversationDto(
    Guid Id,
    ChatContactDto OtherUser,
    ChatMessageDto? LastMessage,
    int UnreadCount,
    DateTimeOffset UpdatedAt);

public sealed record ChatMessageListQuery(
    DateTimeOffset? Before = null,
    int Take = 50);

public sealed record SendChatMessageRequest(
    Guid ReceiverId,
    string Content);

public sealed record ChatReadReceiptDto(
    Guid ConversationId,
    Guid ReaderId,
    DateTimeOffset ReadAt,
    int ReadCount);

public interface IChatRepository
{
    Task<IReadOnlyList<ChatContactDto>> GetContactsAsync(
        Guid userId,
        Guid? tenantId,
        string? keyword,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatConversationDto>> GetConversationsAsync(
        Guid userId,
        Guid? tenantId,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(
        Guid userId,
        Guid? tenantId,
        Guid conversationId,
        ChatMessageListQuery query,
        CancellationToken cancellationToken = default);

    Task<ChatMessageDto> SendAsync(
        Guid userId,
        Guid? tenantId,
        SendChatMessageRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<ChatReadReceiptDto> MarkReadAsync(
        Guid userId,
        Guid? tenantId,
        Guid conversationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public interface IRealtimeChatPublisher
{
    Task PublishMessageAsync(
        ChatMessageDto message,
        CancellationToken cancellationToken = default);

    Task PublishReadReceiptAsync(
        Guid recipientUserId,
        ChatReadReceiptDto receipt,
        CancellationToken cancellationToken = default);
}

public interface IChatAppService
{
    Task<IReadOnlyList<ChatContactDto>> GetContactsAsync(
        string? keyword = null,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatConversationDto>> GetConversationsAsync(
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(
        Guid conversationId,
        ChatMessageListQuery query,
        CancellationToken cancellationToken = default);

    Task<ChatMessageDto> SendAsync(
        SendChatMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<ChatReadReceiptDto> MarkReadAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);
}
