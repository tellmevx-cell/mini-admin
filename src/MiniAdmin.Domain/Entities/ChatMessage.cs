namespace MiniAdmin.Domain.Entities;

public sealed class ChatMessage
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public ChatConversation Conversation { get; set; } = null!;

    public Guid SenderId { get; set; }

    public User Sender { get; set; } = null!;

    public Guid ReceiverId { get; set; }

    public User Receiver { get; set; } = null!;

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ReadAt { get; set; }
}
