namespace MiniAdmin.Domain.Entities;

public sealed class WorkflowAttachment
{
    public Guid Id { get; set; }

    public Guid InstanceId { get; set; }

    public WorkflowInstance Instance { get; set; } = null!;

    public Guid FileId { get; set; }

    public ManagedFile File { get; set; } = null!;

    public string? Remark { get; set; }

    public Guid UploaderUserId { get; set; }

    public string UploaderUserName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
