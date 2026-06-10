namespace MiniAdmin.Application.Contracts.AuditLogs;

public sealed record SaveAuditLogRequest(
    string? UserId,
    string? UserName,
    string Method,
    string Path,
    string? QueryString,
    string Module,
    string Action,
    string? ResourceId,
    int StatusCode,
    bool IsSuccess,
    long ElapsedMilliseconds,
    string? IpAddress,
    string? UserAgent,
    string RequestBody,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    IReadOnlyList<CapturedAuditEntityChange>? EntityChanges = null);
