namespace MiniAdmin.Domain.Entities;

public sealed class NotificationPolicy
{
    public Guid Id { get; set; }

    public string EventCode { get; set; } = string.Empty;

    public string EventName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string RecipientStrategy { get; set; } = "WorkflowDefault";

    public bool EnableInApp { get; set; } = true;

    public bool EnableEmail { get; set; }

    public bool EnableWebhook { get; set; }

    public bool IsEnabled { get; set; } = true;

    public string? Remark { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
