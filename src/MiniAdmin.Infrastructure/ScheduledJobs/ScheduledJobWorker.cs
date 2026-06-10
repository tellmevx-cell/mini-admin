using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniAdmin.Application.Contracts.ScheduledJobs;

namespace MiniAdmin.Infrastructure.ScheduledJobs;

public sealed class ScheduledJobWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ScheduledJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var appService = scope.ServiceProvider.GetRequiredService<IScheduledJobAppService>();
                await appService.RunDueJobsAsync(DateTimeOffset.UtcNow, 5, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduled job worker failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
