namespace MiniAdmin.Domain.Entities;

public sealed class ScheduledJobLogDetail
{
    public Guid Id { get; set; }

    public Guid LogId { get; set; }

    public ScheduledJobLog Log { get; set; } = null!;

    public Guid JobId { get; set; }

    public string JobKey { get; set; } = string.Empty;

    public string DetailType { get; set; } = string.Empty;

    public string TargetType { get; set; } = string.Empty;

    public string? TargetId { get; set; }

    public string? TargetName { get; set; }

    public string? StorageProvider { get; set; }

    public string? StoragePath { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
