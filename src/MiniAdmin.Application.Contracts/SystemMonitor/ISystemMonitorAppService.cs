namespace MiniAdmin.Application.Contracts.SystemMonitor;

public interface ISystemMonitorAppService
{
    Task<SystemMonitorOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
}
