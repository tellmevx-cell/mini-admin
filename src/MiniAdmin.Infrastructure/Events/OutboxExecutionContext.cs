using Microsoft.Extensions.Configuration;
using MiniAdmin.Application.Contracts.Events;

namespace MiniAdmin.Infrastructure.Events;

public sealed class OutboxExecutionContext : IOutboxExecutionContext
{
    public OutboxExecutionContext(IConfiguration configuration)
    {
        var section = configuration.GetSection("Outbox");
        var leaseSeconds = Math.Clamp(ReadInt(section, "LeaseSeconds", 120), 30, 1800);
        var heartbeatSeconds = Math.Clamp(
            ReadInt(section, "HeartbeatSeconds", Math.Max(10, leaseSeconds / 3)),
            5,
            Math.Max(5, leaseSeconds / 2));

        WorkerId = CreateWorkerId();
        LeaseDuration = TimeSpan.FromSeconds(leaseSeconds);
        HeartbeatInterval = TimeSpan.FromSeconds(heartbeatSeconds);
        PollInterval = TimeSpan.FromSeconds(Math.Clamp(ReadInt(section, "PollIntervalSeconds", 2), 1, 60));
        RetryBaseDelay = TimeSpan.FromSeconds(Math.Clamp(ReadInt(section, "RetryBaseSeconds", 5), 1, 300));
        RetryMaxDelay = TimeSpan.FromSeconds(Math.Clamp(ReadInt(section, "RetryMaxSeconds", 900), 30, 86400));
        BatchSize = Math.Clamp(ReadInt(section, "BatchSize", 20), 1, 100);
        DefaultMaxAttempts = Math.Clamp(ReadInt(section, "MaxAttempts", 8), 1, 100);
    }

    public string WorkerId { get; }

    public TimeSpan LeaseDuration { get; }

    public TimeSpan HeartbeatInterval { get; }

    public TimeSpan PollInterval { get; }

    public TimeSpan RetryBaseDelay { get; }

    public TimeSpan RetryMaxDelay { get; }

    public int BatchSize { get; }

    public int DefaultMaxAttempts { get; }

    private static string CreateWorkerId()
    {
        var machine = string.IsNullOrWhiteSpace(Environment.MachineName)
            ? "unknown"
            : Environment.MachineName.Trim();
        var workerId = $"outbox:{machine}:{Environment.ProcessId}:{Guid.NewGuid():N}";
        return workerId.Length <= 128 ? workerId : workerId[..128];
    }

    private static int ReadInt(IConfigurationSection section, string key, int fallback)
    {
        return int.TryParse(section[key], out var value) ? value : fallback;
    }
}
