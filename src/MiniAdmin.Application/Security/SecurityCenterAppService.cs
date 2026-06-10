using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Security;

namespace MiniAdmin.Application.Security;

public sealed class SecurityCenterAppService(
    ISecurityEventRepository securityEventRepository) : ISecurityCenterAppService
{
    public Task<SecurityCenterOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        return securityEventRepository.GetOverviewAsync(cancellationToken);
    }

    public Task<PageResult<SecurityEventDto>> GetEventsAsync(
        SecurityEventListQuery query,
        CancellationToken cancellationToken = default)
    {
        return securityEventRepository.GetEventsAsync(query, cancellationToken);
    }

    public Task RecordEventAsync(
        SaveSecurityEventRequest request,
        CancellationToken cancellationToken = default)
    {
        return securityEventRepository.RecordEventAsync(request, cancellationToken);
    }
}
