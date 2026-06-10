namespace MiniAdmin.Domain.Entities;

public sealed class WorkflowNode
{
    public Guid Id { get; set; }

    public Guid DefinitionId { get; set; }

    public WorkflowDefinition Definition { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string DesignerNodeId { get; set; } = string.Empty;

    public string NodeType { get; set; } = "approve";

    public string ApprovalMode { get; set; } = "Any";

    public int? SlaMinutes { get; set; }

    public string ApproverType { get; set; } = string.Empty;

    public Guid? ApproverUserId { get; set; }

    public User? ApproverUser { get; set; }

    public Guid? ApproverRoleId { get; set; }

    public Role? ApproverRole { get; set; }

    public int Order { get; set; }

    public bool IsEnabled { get; set; } = true;
}
