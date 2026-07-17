using Microsoft.Extensions.Diagnostics.HealthChecks;
using MiniAdmin.Application.Contracts.Caching;

namespace MiniAdmin.Api.Health;

public sealed class PrimaryCacheReadinessHealthCheck(
    IPrimaryCacheHealthProbe healthProbe) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await healthProbe.ProbeAsync(cancellationToken);
            return HealthCheckResult.Healthy("Primary cache is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Primary cache readiness probe failed.", exception);
        }
    }
}
