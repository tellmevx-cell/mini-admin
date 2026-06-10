using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.Alerts;

public sealed record AlertListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Type = null,
    string? Level = null,
    string? Status = null);

public sealed record AlertDto(
    string Id,
    string Type,
    string Level,
    string Title,
    string Content,
    string Source,
    string Status,
    DateTimeOffset FirstTriggeredAt,
    DateTimeOffset LastTriggeredAt,
    DateTimeOffset? RecoveredAt,
    string? AcknowledgedBy,
    DateTimeOffset? AcknowledgedAt,
    string? AcknowledgeRemark,
    int TriggerCount);

public sealed record AcknowledgeAlertRequest(string? Remark);

public sealed record AlertSignal(
    string Type,
    string Level,
    string Title,
    string Content,
    string Source,
    bool NotifyEnabled = true);

public sealed record AlertScanResultDto(
    int ActiveSignalCount,
    int CreatedCount,
    int UpdatedCount,
    int RecoveredCount,
    IReadOnlyList<AlertDto> CreatedAlerts);

public interface IAlertRepository
{
    Task<PageResult<AlertDto>> GetListAsync(
        AlertListQuery query,
        CancellationToken cancellationToken = default);

    Task<AlertDto?> AcknowledgeAsync(
        Guid id,
        string userName,
        string? remark,
        CancellationToken cancellationToken = default);

    Task<AlertScanResultDto> SaveScanResultAsync(
        IReadOnlyList<AlertSignal> signals,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public interface IAlertAppService
{
    Task<PageResult<AlertDto>> GetListAsync(
        AlertListQuery query,
        CancellationToken cancellationToken = default);

    Task<AlertDto?> AcknowledgeAsync(
        Guid id,
        string userName,
        AcknowledgeAlertRequest request,
        CancellationToken cancellationToken = default);

    Task<AlertScanResultDto> ScanAsync(CancellationToken cancellationToken = default);
}
