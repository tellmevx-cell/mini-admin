namespace MiniAdmin.Domain.Entities;

public sealed class TenantResourceQuotaWarning
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string ResourceType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public long UsedValue { get; set; }

    public long LimitValue { get; set; }

    public int NotificationSequence { get; set; }

    public string? LastNotifiedStatus { get; set; }

    public DateTimeOffset? LastNotifiedAt { get; set; }

    public DateTimeOffset LastCheckedAt { get; set; } = DateTimeOffset.UtcNow;
}
