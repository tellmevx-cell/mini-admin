namespace MiniAdmin.Domain.Entities;

public sealed class WorkflowTask
{
    public Guid Id { get; set; }

    public Guid InstanceId { get; set; }

    public WorkflowInstance Instance { get; set; } = null!;

    public Guid NodeId { get; set; }

    public WorkflowNode Node { get; set; } = null!;

    public string NodeName { get; set; } = string.Empty;

    public Guid ApproverUserId { get; set; }

    public User ApproverUser { get; set; } = null!;

    public string ApproverUserName { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? DueAt { get; set; }

    public DateTimeOffset? LastAutoRemindedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
