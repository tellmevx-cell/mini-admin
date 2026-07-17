namespace MiniAdmin.Domain.Entities;

public sealed class InboxMessage
{
    public Guid Id { get; set; }

    public Guid MessageId { get; set; }

    public string ConsumerName { get; set; } = string.Empty;

    public DateTimeOffset ProcessedAt { get; set; }
}
