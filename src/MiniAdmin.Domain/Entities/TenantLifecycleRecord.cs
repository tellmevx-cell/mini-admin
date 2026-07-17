namespace MiniAdmin.Domain.Entities;

public sealed class TenantLifecycleRecord
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public Guid? OperatorUserId { get; set; }

    public string? OperatorUserName { get; set; }

    public string? FromStatus { get; set; }

    public string? ToStatus { get; set; }

    public DateTimeOffset? PreviousExpireAt { get; set; }

    public DateTimeOffset? NewExpireAt { get; set; }

    public Guid? PreviousPackageId { get; set; }

    public Guid? NewPackageId { get; set; }

    public int? ReminderDays { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? DeduplicationKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
