using MiniAdmin.Domain.Shared.MultiTenancy;

namespace MiniAdmin.Domain.Entities;

public sealed class WorkflowDefinition : IHasTenant
{
    public const string DraftStatus = "Draft";

    public const string PublishedStatus = "Published";

    public const string ArchivedStatus = "Archived";

    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? FormName { get; set; }

    public string? Description { get; set; }

    public string DesignerJson { get; set; } = string.Empty;

    public string FormSchemaJson { get; set; } = "[]";

    public bool IsEnabled { get; set; } = true;

    public int Version { get; set; } = 1;

    public string PublishStatus { get; set; } = DraftStatus;

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<WorkflowNode> Nodes { get; set; } = [];
}
