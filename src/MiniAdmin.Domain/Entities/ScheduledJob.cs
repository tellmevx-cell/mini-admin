namespace MiniAdmin.Domain.Entities;

public sealed class ScheduledJob
{
    public Guid Id { get; set; }

    public string JobKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int IntervalSeconds { get; set; }

    public bool IsEnabled { get; set; } = true;

    public string LastStatus { get; set; } = "Never";

    public string? LastMessage { get; set; }

    public DateTimeOffset? LastRunAt { get; set; }

    public DateTimeOffset? NextRunAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ScheduledJobLog> Logs { get; set; } = [];
}
