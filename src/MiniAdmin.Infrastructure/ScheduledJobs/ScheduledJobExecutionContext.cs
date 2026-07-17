using Microsoft.Extensions.Configuration;
using MiniAdmin.Application.Contracts.ScheduledJobs;

namespace MiniAdmin.Infrastructure.ScheduledJobs;

public sealed class ScheduledJobExecutionContext : IScheduledJobExecutionContext
{
    public ScheduledJobExecutionContext(IConfiguration configuration)
    {
        var section = configuration.GetSection("ScheduledJobs");
        var leaseSeconds = Math.Clamp(ReadInt(section, "LeaseSeconds", 120), 30, 1800);
        var heartbeatSeconds = Math.Clamp(
            ReadInt(section, "HeartbeatSeconds", Math.Max(10, leaseSeconds / 3)),
            5,
            Math.Max(5, leaseSeconds / 2));

        WorkerId = CreateWorkerId();
        LeaseDuration = TimeSpan.FromSeconds(leaseSeconds);
        HeartbeatInterval = TimeSpan.FromSeconds(heartbeatSeconds);
        PollInterval = TimeSpan.FromSeconds(Math.Clamp(ReadInt(section, "PollIntervalSeconds", 30), 1, 300));
        BatchSize = Math.Clamp(ReadInt(section, "BatchSize", 5), 1, 20);
    }

    public string WorkerId { get; }

    public TimeSpan LeaseDuration { get; }

    public TimeSpan HeartbeatInterval { get; }

    public TimeSpan PollInterval { get; }

    public int BatchSize { get; }

    private static string CreateWorkerId()
    {
        var machine = string.IsNullOrWhiteSpace(Environment.MachineName)
            ? "unknown"
            : Environment.MachineName.Trim();
        var workerId = $"{machine}:{Environment.ProcessId}:{Guid.NewGuid():N}";
        return workerId.Length <= 128 ? workerId : workerId[..128];
    }

    private static int ReadInt(IConfigurationSection section, string key, int fallback)
    {
        return int.TryParse(section[key], out var value) ? value : fallback;
    }
}
