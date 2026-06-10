namespace MiniAdmin.Domain.Entities;

public sealed class Alert
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Status { get; set; } = "Active";

    public DateTimeOffset FirstTriggeredAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastTriggeredAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? RecoveredAt { get; set; }

    public string? AcknowledgedBy { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }

    public string? AcknowledgeRemark { get; set; }

    public int TriggerCount { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
