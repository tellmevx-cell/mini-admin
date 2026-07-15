using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace MiniAdmin.Gateway;

public enum GatewayCircuitState
{
    Closed,
    Open,
    HalfOpen
}

public sealed class GatewayCircuitBreakerOptions
{
    public const string SectionName = "CircuitBreaker";

    public bool Enabled { get; set; }

    public int FailureThreshold { get; set; } = 5;

    public int BreakDurationSeconds { get; set; } = 30;
}

public sealed record GatewayCircuitLease(
    bool Allowed,
    bool IsProbe,
    GatewayCircuitState State,
    TimeSpan? RetryAfter);

public sealed class GatewayCircuitBreaker(
    IOptions<GatewayCircuitBreakerOptions> options,
    TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, CircuitEntry> circuits =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly GatewayCircuitBreakerOptions options = options.Value;

    public GatewayCircuitLease TryAcquire(string clusterId)
    {
        if (!options.Enabled)
        {
            return new GatewayCircuitLease(true, false, GatewayCircuitState.Closed, null);
        }

        var entry = circuits.GetOrAdd(clusterId, _ => new CircuitEntry());
        lock (entry.SyncRoot)
        {
            var now = timeProvider.GetUtcNow();
            if (entry.State == GatewayCircuitState.Open)
            {
                var retryAt = entry.OpenedAt.AddSeconds(Math.Max(options.BreakDurationSeconds, 1));
                if (now < retryAt)
                {
                    return new GatewayCircuitLease(
                        false,
                        false,
                        entry.State,
                        retryAt - now);
                }

                entry.State = GatewayCircuitState.HalfOpen;
                entry.ProbeInFlight = false;
            }

            if (entry.State == GatewayCircuitState.HalfOpen)
            {
                if (entry.ProbeInFlight)
                {
                    return new GatewayCircuitLease(
                        false,
                        false,
                        entry.State,
                        TimeSpan.FromSeconds(1));
                }

                entry.ProbeInFlight = true;
                return new GatewayCircuitLease(true, true, entry.State, null);
            }

            return new GatewayCircuitLease(true, false, entry.State, null);
        }
    }

    public void Report(string clusterId, GatewayCircuitLease lease, bool success)
    {
        if (!options.Enabled || !lease.Allowed)
        {
            return;
        }

        var entry = circuits.GetOrAdd(clusterId, _ => new CircuitEntry());
        lock (entry.SyncRoot)
        {
            if (lease.IsProbe)
            {
                entry.ProbeInFlight = false;
                if (success)
                {
                    entry.State = GatewayCircuitState.Closed;
                    entry.ConsecutiveFailures = 0;
                }
                else
                {
                    Open(entry);
                }

                return;
            }

            if (entry.State != GatewayCircuitState.Closed)
            {
                return;
            }

            if (success)
            {
                entry.ConsecutiveFailures = 0;
                return;
            }

            entry.ConsecutiveFailures++;
            if (entry.ConsecutiveFailures >= Math.Max(options.FailureThreshold, 1))
            {
                Open(entry);
            }
        }
    }

    public GatewayCircuitState GetState(string clusterId)
    {
        return circuits.TryGetValue(clusterId, out var entry)
            ? entry.State
            : GatewayCircuitState.Closed;
    }

    private void Open(CircuitEntry entry)
    {
        entry.State = GatewayCircuitState.Open;
        entry.OpenedAt = timeProvider.GetUtcNow();
        entry.ProbeInFlight = false;
    }

    private sealed class CircuitEntry
    {
        public object SyncRoot { get; } = new();

        public GatewayCircuitState State { get; set; } = GatewayCircuitState.Closed;

        public int ConsecutiveFailures { get; set; }

        public DateTimeOffset OpenedAt { get; set; }

        public bool ProbeInFlight { get; set; }
    }
}
