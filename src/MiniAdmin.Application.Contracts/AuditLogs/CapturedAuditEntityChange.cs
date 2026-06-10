namespace MiniAdmin.Application.Contracts.AuditLogs;

public sealed record CapturedAuditEntityChange(
    string EntityName,
    string EntityId,
    string OperationType,
    string? BeforeJson,
    string? AfterJson,
    string DiffJson,
    DateTimeOffset CreatedAt);
