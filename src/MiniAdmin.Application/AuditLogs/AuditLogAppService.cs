using MiniAdmin.Application.Contracts.AuditLogs;
using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.AuditLogs;

public sealed class AuditLogAppService(IAuditLogRepository auditLogRepository) : IAuditLogAppService
{
    public Task<PageResult<AuditLogDto>> GetListAsync(
        AuditLogListQuery query,
        CancellationToken cancellationToken = default)
    {
        return auditLogRepository.GetListAsync(query, cancellationToken);
    }

    public Task<IReadOnlyList<AuditLogDto>> GetExportListAsync(
        AuditLogListQuery query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return auditLogRepository.GetExportListAsync(query, limit, cancellationToken);
    }
}
