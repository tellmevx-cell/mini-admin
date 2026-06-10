namespace MiniAdmin.Application.Contracts.AuditLogs;

public sealed record AuditEntityChangeDto(
    string Id,
    string AuditLogId,
    string EntityName,
    string EntityId,
    string OperationType,
    string? BeforeJson,
    string? AfterJson,
    string DiffJson,
    DateTimeOffset CreatedAt);
