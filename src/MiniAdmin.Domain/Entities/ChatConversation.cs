namespace MiniAdmin.Domain.Entities;

public sealed class ChatConversation
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public string TenantScopeKey { get; set; } = string.Empty;

    public Guid ParticipantOneId { get; set; }

    public User ParticipantOne { get; set; } = null!;

    public Guid ParticipantTwoId { get; set; }

    public User ParticipantTwo { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ChatMessage> Messages { get; set; } = [];
}
