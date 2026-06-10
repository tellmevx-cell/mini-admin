namespace MiniAdmin.Domain.Entities;

public sealed class AuditEntityChange
{
    public Guid Id { get; set; }

    public Guid AuditLogId { get; set; }

    public AuditLog? AuditLog { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string OperationType { get; set; } = string.Empty;

    public string? BeforeJson { get; set; }

    public string? AfterJson { get; set; }

    public string DiffJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
