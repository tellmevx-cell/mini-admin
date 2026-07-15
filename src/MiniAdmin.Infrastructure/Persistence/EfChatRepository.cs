using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Chat;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfChatRepository(
    MiniAdminDbContext dbContext,
    IRealtimeChatPublisher realtimePublisher) : IChatRepository
{
    public async Task<IReadOnlyList<ChatContactDto>> GetContactsAsync(
        Guid userId,
        Guid? tenantId,
        string? keyword,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id != userId && user.IsEnabled && user.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            query = query.Where(user =>
                user.UserName.Contains(normalized) || user.RealName.Contains(normalized));
        }

        return await query
            .OrderBy(user => user.RealName)
            .ThenBy(user => user.UserName)
            .Take(Math.Clamp(take, 1, 100))
            .Select(user => new ChatContactDto(user.Id, user.UserName, user.RealName))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatConversationDto>> GetConversationsAsync(
        Guid userId,
        Guid? tenantId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var conversations = await dbContext.ChatConversations
            .AsNoTracking()
            .Where(conversation =>
                conversation.TenantScopeKey == ScopeKey(tenantId) &&
                (conversation.ParticipantOneId == userId || conversation.ParticipantTwoId == userId))
            .Include(conversation => conversation.ParticipantOne)
            .Include(conversation => conversation.ParticipantTwo)
            .Include(conversation => conversation.Messages)
                .ThenInclude(message => message.Sender)
            .Include(conversation => conversation.Messages)
                .ThenInclude(message => message.Receiver)
            .OrderByDescending(conversation => conversation.UpdatedAt)
            .Take(Math.Clamp(take, 1, 100))
            .ToArrayAsync(cancellationToken);

        return conversations.Select(conversation =>
        {
            var other = conversation.ParticipantOneId == userId
                ? conversation.ParticipantTwo
                : conversation.ParticipantOne;
            var lastMessage = conversation.Messages
                .OrderByDescending(message => message.CreatedAt)
                .FirstOrDefault();
            return new ChatConversationDto(
                conversation.Id,
                new ChatContactDto(other.Id, other.UserName, other.RealName),
                lastMessage is null ? null : ToDto(lastMessage),
                conversation.Messages.Count(message => message.ReceiverId == userId && message.ReadAt == null),
                conversation.UpdatedAt);
        }).ToArray();
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(
        Guid userId,
        Guid? tenantId,
        Guid conversationId,
        ChatMessageListQuery query,
        CancellationToken cancellationToken = default)
    {
        await RequireConversationAsync(userId, tenantId, conversationId, cancellationToken);
        var messages = dbContext.ChatMessages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId);
        if (query.Before.HasValue)
        {
            messages = messages.Where(message => message.CreatedAt < query.Before.Value);
        }

        return await messages
            .OrderByDescending(message => message.CreatedAt)
            .Take(Math.Clamp(query.Take, 1, 100))
            .Include(message => message.Sender)
            .Include(message => message.Receiver)
            .Select(message => ToDto(message))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ChatMessageDto> SendAsync(
        Guid userId,
        Guid? tenantId,
        SendChatMessageRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content.Length > 2000)
        {
            throw new InvalidOperationException("消息内容长度必须在 1 到 2000 个字符之间。");
        }

        if (request.ReceiverId == userId)
        {
            throw new InvalidOperationException("不能给自己发送聊天消息。");
        }

        var users = await dbContext.Users
            .Where(user =>
                (user.Id == userId || user.Id == request.ReceiverId) &&
                user.IsEnabled &&
                user.TenantId == tenantId)
            .ToDictionaryAsync(user => user.Id, cancellationToken);
        if (!users.ContainsKey(userId) || !users.ContainsKey(request.ReceiverId))
        {
            throw new InvalidOperationException("接收人不存在、已停用或不属于当前租户。");
        }

        var (participantOneId, participantTwoId) = SortParticipants(userId, request.ReceiverId);
        var scopeKey = ScopeKey(tenantId);
        var conversation = await dbContext.ChatConversations.SingleOrDefaultAsync(
            item =>
                item.TenantScopeKey == scopeKey &&
                item.ParticipantOneId == participantOneId &&
                item.ParticipantTwoId == participantTwoId,
            cancellationToken);
        if (conversation is null)
        {
            conversation = new ChatConversation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TenantScopeKey = scopeKey,
                ParticipantOneId = participantOneId,
                ParticipantTwoId = participantTwoId,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.ChatConversations.Add(conversation);
        }
        else
        {
            conversation.UpdatedAt = now;
        }

        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            SenderId = userId,
            ReceiverId = request.ReceiverId,
            Content = content,
            CreatedAt = now
        };
        dbContext.ChatMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        var result = new ChatMessageDto(
            message.Id,
            conversation.Id,
            userId,
            users[userId].RealName,
            request.ReceiverId,
            users[request.ReceiverId].RealName,
            message.Content,
            message.CreatedAt,
            null);
        await realtimePublisher.PublishMessageAsync(result, cancellationToken);
        return result;
    }

    public async Task<ChatReadReceiptDto> MarkReadAsync(
        Guid userId,
        Guid? tenantId,
        Guid conversationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var conversation = await RequireConversationAsync(
            userId,
            tenantId,
            conversationId,
            cancellationToken);
        var unread = await dbContext.ChatMessages
            .Where(message =>
                message.ConversationId == conversationId &&
                message.ReceiverId == userId &&
                message.ReadAt == null)
            .ToArrayAsync(cancellationToken);
        foreach (var message in unread)
        {
            message.ReadAt = now;
        }

        if (unread.Length > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var receipt = new ChatReadReceiptDto(conversationId, userId, now, unread.Length);
        var otherUserId = conversation.ParticipantOneId == userId
            ? conversation.ParticipantTwoId
            : conversation.ParticipantOneId;
        await realtimePublisher.PublishReadReceiptAsync(otherUserId, receipt, cancellationToken);
        return receipt;
    }

    private async Task<ChatConversation> RequireConversationAsync(
        Guid userId,
        Guid? tenantId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.ChatConversations.SingleOrDefaultAsync(
            item =>
                item.Id == conversationId &&
                item.TenantScopeKey == ScopeKey(tenantId) &&
                (item.ParticipantOneId == userId || item.ParticipantTwoId == userId),
            cancellationToken);
        return conversation ?? throw new KeyNotFoundException("聊天会话不存在或无权访问。");
    }

    private static ChatMessageDto ToDto(ChatMessage message)
    {
        return new ChatMessageDto(
            message.Id,
            message.ConversationId,
            message.SenderId,
            message.Sender.RealName,
            message.ReceiverId,
            message.Receiver.RealName,
            message.Content,
            message.CreatedAt,
            message.ReadAt);
    }

    private static (Guid ParticipantOneId, Guid ParticipantTwoId) SortParticipants(
        Guid first,
        Guid second)
    {
        return first.CompareTo(second) < 0 ? (first, second) : (second, first);
    }

    private static string ScopeKey(Guid? tenantId)
    {
        return tenantId?.ToString("N") ?? "platform";
    }
}
