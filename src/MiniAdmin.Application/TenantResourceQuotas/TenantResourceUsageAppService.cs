using MiniAdmin.Application.Contracts.TenantResourceQuotas;
using MiniAdmin.Platform.DynamicApi;

namespace MiniAdmin.Application.TenantResourceQuotas;

[DynamicApi("tenant/resource-usage", Name = "TenantResourceUsage", Tag = "租户资源")]
public sealed class TenantResourceUsageAppService(
    ITenantResourceQuotaWarningService warningService)
{
    [DynamicGet(
        "",
        Resource = "tenant.resource-usage",
        Action = "query",
        OperationId = "GetCurrentTenantResourceUsage",
        Summary = "查询当前租户资源用量与配额状态")]
    public Task<TenantResourceUsageDto?> GetAsync(
        CancellationToken cancellationToken = default)
    {
        return warningService.GetCurrentUsageAsync(cancellationToken);
    }
}
