namespace MiniAdmin.Domain.Entities;

public sealed class WorkflowCcRecord
{
    public Guid Id { get; set; }

    public Guid InstanceId { get; set; }

    public WorkflowInstance Instance { get; set; } = null!;

    public Guid? NodeId { get; set; }

    public string? NodeName { get; set; }

    public Guid RecipientUserId { get; set; }

    public User RecipientUser { get; set; } = null!;

    public string RecipientUserName { get; set; } = string.Empty;

    public Guid? SenderUserId { get; set; }

    public string? SenderUserName { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ReadAt { get; set; }
}
