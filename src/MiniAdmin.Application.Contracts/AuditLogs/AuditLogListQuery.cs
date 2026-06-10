namespace MiniAdmin.Application.Contracts.AuditLogs;

public sealed record AuditLogListQuery(
    int Page = 1,
    int PageSize = 20,
    string? UserName = null,
    string? Method = null,
    string? Path = null,
    string? Module = null,
    string? Action = null,
    bool? IsSuccess = null,
    DateTimeOffset? StartCreatedAt = null,
    DateTimeOffset? EndCreatedAt = null,
    string? CurrentUserName = null);
