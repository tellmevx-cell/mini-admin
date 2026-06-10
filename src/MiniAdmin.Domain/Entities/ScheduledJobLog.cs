namespace MiniAdmin.Domain.Entities;

public sealed class ScheduledJobLog
{
    public Guid Id { get; set; }

    public Guid JobId { get; set; }

    public ScheduledJob Job { get; set; } = null!;

    public string JobKey { get; set; } = string.Empty;

    public string JobName { get; set; } = string.Empty;

    public string TriggerType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset FinishedAt { get; set; }

    public long ElapsedMilliseconds { get; set; }

    public List<ScheduledJobLogDetail> Details { get; set; } = [];
}
