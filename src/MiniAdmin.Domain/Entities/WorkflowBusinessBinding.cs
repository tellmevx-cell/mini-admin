using MiniAdmin.Domain.Shared.MultiTenancy;

namespace MiniAdmin.Domain.Entities;

public sealed class WorkflowBusinessBinding : IHasTenant
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public string BusinessType { get; set; } = string.Empty;

    public string BusinessName { get; set; } = string.Empty;

    public Guid DefinitionId { get; set; }

    public WorkflowDefinition Definition { get; set; } = null!;

    public bool IsEnabled { get; set; } = true;

    public string? Remark { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
