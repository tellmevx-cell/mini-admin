namespace MiniAdmin.Domain.Entities;

public sealed class AlertRuleRecipient
{
    public Guid Id { get; set; }

    public Guid AlertRuleId { get; set; }

    public AlertRule AlertRule { get; set; } = null!;

    public string RecipientType { get; set; } = string.Empty;

    public Guid RecipientId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
