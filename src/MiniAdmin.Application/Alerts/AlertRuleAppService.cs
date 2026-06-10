using MiniAdmin.Application.Contracts.Alerts;
using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Alerts;

public sealed class AlertRuleAppService(IAlertRuleRepository alertRuleRepository) : IAlertRuleAppService
{
    public Task<PageResult<AlertRuleDto>> GetListAsync(
        AlertRuleListQuery query,
        CancellationToken cancellationToken = default)
    {
        return alertRuleRepository.GetListAsync(query, cancellationToken);
    }

    public Task<AlertRuleDto?> UpdateAsync(
        Guid id,
        UpdateAlertRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        return alertRuleRepository.UpdateAsync(id, request, cancellationToken);
    }
}
