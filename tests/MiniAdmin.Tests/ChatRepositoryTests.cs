using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Chat;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Tests;

public sealed class ChatRepositoryTests
{
    [Fact]
    public async Task Chat_flow_persists_messages_and_publishes_read_receipt()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var sender = CreateUser("sender", "发送人", tenantId);
        var receiver = CreateUser("receiver", "接收人", tenantId);
        dbContext.Users.AddRange(sender, receiver);
        await dbContext.SaveChangesAsync();
        var publisher = new RecordingChatPublisher();
        var repository = new EfChatRepository(dbContext, publisher);
        var sentAt = DateTimeOffset.UtcNow;

        var sent = await repository.SendAsync(
            sender.Id,
            tenantId,
            new SendChatMessageRequest(receiver.Id, "  hello  "),
            sentAt);
        var conversations = await repository.GetConversationsAsync(receiver.Id, tenantId, 20);
        var messages = await repository.GetMessagesAsync(
            receiver.Id,
            tenantId,
            sent.ConversationId,
            new ChatMessageListQuery());
        var receipt = await repository.MarkReadAsync(
            receiver.Id,
            tenantId,
            sent.ConversationId,
            sentAt.AddMinutes(1));

        Assert.Equal("hello", sent.Content);
        Assert.Single(conversations);
        Assert.Equal(sender.Id, conversations[0].OtherUser.Id);
        Assert.Equal(1, conversations[0].UnreadCount);
        Assert.Single(messages);
        Assert.Equal(sent.Id, messages[0].Id);
        Assert.Equal(1, receipt.ReadCount);
        Assert.Equal(sent.Id, publisher.Message?.Id);
        Assert.Equal(sender.Id, publisher.ReadReceiptRecipient);
        Assert.Equal(receipt, publisher.ReadReceipt);
    }

    [Fact]
    public async Task Send_rejects_receiver_from_another_tenant()
    {
        await using var dbContext = CreateDbContext();
        var sender = CreateUser("sender", "发送人", Guid.NewGuid());
        var receiver = CreateUser("receiver", "接收人", Guid.NewGuid());
        dbContext.Users.AddRange(sender, receiver);
        await dbContext.SaveChangesAsync();
        var repository = new EfChatRepository(dbContext, new RecordingChatPublisher());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SendAsync(
                sender.Id,
                sender.TenantId,
                new SendChatMessageRequest(receiver.Id, "blocked"),
                DateTimeOffset.UtcNow));

        Assert.Contains("当前租户", exception.Message);
        Assert.Empty(dbContext.ChatMessages);
    }

    [Fact]
    public async Task History_rejects_user_who_is_not_a_conversation_participant()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var sender = CreateUser("sender", "发送人", tenantId);
        var receiver = CreateUser("receiver", "接收人", tenantId);
        var intruder = CreateUser("intruder", "无关用户", tenantId);
        dbContext.Users.AddRange(sender, receiver, intruder);
        await dbContext.SaveChangesAsync();
        var repository = new EfChatRepository(dbContext, new RecordingChatPublisher());
        var message = await repository.SendAsync(
            sender.Id,
            tenantId,
            new SendChatMessageRequest(receiver.Id, "private"),
            DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            repository.GetMessagesAsync(
                intruder.Id,
                tenantId,
                message.ConversationId,
                new ChatMessageListQuery()));
    }

    private static MiniAdminDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MiniAdminDbContext>()
            .UseInMemoryDatabase($"chat-tests-{Guid.NewGuid():N}")
            .Options;
        return new MiniAdminDbContext(options);
    }

    private static User CreateUser(string userName, string realName, Guid? tenantId)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = userName,
            RealName = realName,
            PasswordHash = "test",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            IsEnabled = true
        };
    }

    private sealed class RecordingChatPublisher : IRealtimeChatPublisher
    {
        public ChatMessageDto? Message { get; private set; }

        public Guid? ReadReceiptRecipient { get; private set; }

        public ChatReadReceiptDto? ReadReceipt { get; private set; }

        public Task PublishMessageAsync(
            ChatMessageDto message,
            CancellationToken cancellationToken = default)
        {
            Message = message;
            return Task.CompletedTask;
        }

        public Task PublishReadReceiptAsync(
            Guid recipientUserId,
            ChatReadReceiptDto receipt,
            CancellationToken cancellationToken = default)
        {
            ReadReceiptRecipient = recipientUserId;
            ReadReceipt = receipt;
            return Task.CompletedTask;
        }
    }
}
