using MiniAdmin.Domain.Shared.MultiTenancy;

namespace MiniAdmin.Domain.Entities;

public sealed class WorkflowInstance : IHasTenant
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public Guid DefinitionId { get; set; }

    public WorkflowDefinition Definition { get; set; } = null!;

    public string DefinitionCode { get; set; } = string.Empty;

    public string DefinitionName { get; set; } = string.Empty;

    public int DefinitionVersion { get; set; } = 1;

    public string DefinitionSnapshotJson { get; set; } = "{}";

    public string Title { get; set; } = string.Empty;

    public string? BusinessKey { get; set; }

    public string FormDataJson { get; set; } = "{}";

    public string Status { get; set; } = "Pending";

    public Guid? CurrentNodeId { get; set; }

    public string? CurrentNodeName { get; set; }

    public Guid InitiatorUserId { get; set; }

    public User InitiatorUser { get; set; } = null!;

    public string InitiatorUserName { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public List<WorkflowTask> Tasks { get; set; } = [];

    public List<WorkflowActionLog> ActionLogs { get; set; } = [];

    public List<WorkflowCcRecord> CcRecords { get; set; } = [];

    public List<WorkflowAttachment> Attachments { get; set; } = [];

    public List<WorkflowComment> Comments { get; set; } = [];
}
