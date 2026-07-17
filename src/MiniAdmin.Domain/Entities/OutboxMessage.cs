namespace MiniAdmin.Domain.Entities;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public int AttemptCount { get; set; }

    public int MaxAttempts { get; set; } = 8;

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public Guid? TenantId { get; set; }

    public string? CorrelationId { get; set; }

    public Guid? LeaseToken { get; set; }

    public string? LeaseOwner { get; set; }

    public DateTimeOffset? LeaseExpiresAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
