namespace MiniAdmin.Application.Contracts.Caching;

public interface IPrimaryCacheHealthProbe
{
    Task ProbeAsync(CancellationToken cancellationToken = default);
}
