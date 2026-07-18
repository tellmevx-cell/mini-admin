using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MiniAdmin.Gateway;
using Yarp.ReverseProxy.Forwarder;

namespace MiniAdmin.Tests;

public sealed class GatewayGovernanceTests
{
    [Fact]
    public void CanaryDecision_Honors_Explicit_Header_And_Tenant_Whitelist()
    {
        var options = Options.Create(new GatewayCanaryOptions
        {
            Enabled = true,
            Percentage = 0,
            TenantWhitelist = ["tenant-a"]
        });
        var service = new CanaryDecisionService(options);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Headers["X-Tenant-Id"] = "tenant-a";

        Assert.True(service.Decide(context).UseCanary);

        context.Request.Headers["X-Release-Channel"] = "stable";
        Assert.False(service.Decide(context).UseCanary);

        context.Request.Headers["X-Release-Channel"] = "canary";
        Assert.True(service.Decide(context).UseCanary);
    }

    [Fact]
    public void CanaryDecision_Percentage_Is_Stable_For_The_Same_Routing_Key()
    {
        var options = Options.Create(new GatewayCanaryOptions
        {
            Enabled = true,
            Percentage = 50
        });
        var service = new CanaryDecisionService(options);
        var first = new DefaultHttpContext();
        var second = new DefaultHttpContext();
        first.Request.Headers["X-Routing-Key"] = "customer-42";
        second.Request.Headers["X-Routing-Key"] = "customer-42";

        Assert.Equal(service.Decide(first), service.Decide(second));
    }

    [Fact]
    public void CircuitBreaker_Transitions_Closed_Open_HalfOpen_Closed()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-15T00:00:00Z"));
        var breaker = new GatewayCircuitBreaker(
            Options.Create(new GatewayCircuitBreakerOptions
            {
                Enabled = true,
                FailureThreshold = 2,
                BreakDurationSeconds = 30
            }),
            clock);

        var first = breaker.TryAcquire("api");
        breaker.Report("api", first, success: false);
        var second = breaker.TryAcquire("api");
        breaker.Report("api", second, success: false);

        Assert.Equal(GatewayCircuitState.Open, breaker.GetState("api"));
        Assert.False(breaker.TryAcquire("api").Allowed);

        clock.Advance(TimeSpan.FromSeconds(31));
        var probe = breaker.TryAcquire("api");
        Assert.True(probe.Allowed);
        Assert.True(probe.IsProbe);
        Assert.False(breaker.TryAcquire("api").Allowed);

        breaker.Report("api", probe, success: true);
        Assert.Equal(GatewayCircuitState.Closed, breaker.GetState("api"));
        Assert.True(breaker.TryAcquire("api").Allowed);
    }

    [Fact]
    public void CircuitFailurePolicy_Only_Classifies_Transient_Upstream_Failures()
    {
        Assert.False(GatewayCircuitFailurePolicy.IsTransientFailure(null, 500));
        Assert.False(GatewayCircuitFailurePolicy.IsTransientFailure(null, 429));
        Assert.True(GatewayCircuitFailurePolicy.IsTransientFailure(null, 502));
        Assert.True(GatewayCircuitFailurePolicy.IsTransientFailure(null, 503));
        Assert.True(GatewayCircuitFailurePolicy.IsTransientFailure(null, 504));
        Assert.True(GatewayCircuitFailurePolicy.IsTransientFailure((ForwarderError)999, 200));
        Assert.False(GatewayCircuitFailurePolicy.IsTransientFailure(
            ForwarderError.RequestCanceled,
            503));
    }

    private sealed class ManualTimeProvider(DateTimeOffset current) : TimeProvider
    {
        private DateTimeOffset current = current;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration)
        {
            current = current.Add(duration);
        }
    }
}
