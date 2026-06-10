using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.AuditLogs;

public interface IAuditLogRepository
{
    Task<PageResult<AuditLogDto>> GetListAsync(
        AuditLogListQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogDto>> GetExportListAsync(
        AuditLogListQuery query,
        int limit,
        CancellationToken cancellationToken = default);

    Task CreateAsync(
        SaveAuditLogRequest request,
        CancellationToken cancellationToken = default);
}
