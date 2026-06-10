namespace MiniAdmin.Domain.Entities;

public sealed class WorkflowComment
{
    public Guid Id { get; set; }

    public Guid InstanceId { get; set; }

    public WorkflowInstance Instance { get; set; } = null!;

    public string Content { get; set; } = string.Empty;

    public Guid AuthorUserId { get; set; }

    public string AuthorUserName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
