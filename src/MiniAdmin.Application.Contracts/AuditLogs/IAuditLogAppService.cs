using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.AuditLogs;

public interface IAuditLogAppService
{
    Task<PageResult<AuditLogDto>> GetListAsync(
        AuditLogListQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogDto>> GetExportListAsync(
        AuditLogListQuery query,
        int limit,
        CancellationToken cancellationToken = default);
}
