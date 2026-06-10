namespace MiniAdmin.Domain.Entities;

public sealed class AlertRule
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Metric { get; set; } = string.Empty;

    public string Operator { get; set; } = ">=";

    public decimal Threshold { get; set; }

    public int WindowMinutes { get; set; } = 1440;

    public string Level { get; set; } = "Warning";

    public bool Enabled { get; set; } = true;

    public bool NotifyEnabled { get; set; } = true;

    public bool EmailEnabled { get; set; }

    public List<AlertRuleRecipient> Recipients { get; set; } = [];

    public int Sort { get; set; }

    public string? Remark { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
