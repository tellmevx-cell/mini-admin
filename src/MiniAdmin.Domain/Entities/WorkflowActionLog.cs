namespace MiniAdmin.Domain.Entities;

public sealed class WorkflowActionLog
{
    public Guid Id { get; set; }

    public Guid InstanceId { get; set; }

    public WorkflowInstance Instance { get; set; } = null!;

    public Guid? NodeId { get; set; }

    public string? NodeName { get; set; }

    public string Action { get; set; } = string.Empty;

    public Guid OperatorUserId { get; set; }

    public string OperatorUserName { get; set; } = string.Empty;

    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
