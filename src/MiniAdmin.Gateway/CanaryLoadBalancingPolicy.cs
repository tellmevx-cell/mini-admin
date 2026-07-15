using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;

namespace MiniAdmin.Gateway;

public sealed class CanaryLoadBalancingPolicy(
    CanaryDecisionService decisionService,
    IOptions<GatewayCanaryOptions> options) : ILoadBalancingPolicy
{
    public const string PolicyName = "MiniAdminCanary";

    private readonly GatewayCanaryOptions options = options.Value;

    public string Name => PolicyName;

    public DestinationState? PickDestination(
        HttpContext context,
        ClusterState cluster,
        IReadOnlyList<DestinationState> availableDestinations)
    {
        if (availableDestinations.Count == 0)
        {
            return null;
        }

        var decision = decisionService.Decide(context);
        var preferredRelease = decision.UseCanary
            ? options.CanaryDestinationValue
            : options.StableDestinationValue;
        var preferred = availableDestinations
            .Where(destination => GetRelease(destination)
                .Equals(preferredRelease, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var candidates = preferred.Length > 0 ? preferred : availableDestinations.ToArray();
        var selected = candidates
            .OrderBy(destination => destination.ConcurrentRequestCount)
            .ThenBy(destination => StableIndex(decision.RoutingKey, destination.DestinationId))
            .ThenBy(destination => destination.DestinationId, StringComparer.Ordinal)
            .First();
        var selectedRelease = GetRelease(selected);

        context.Request.Headers["X-Gateway-Release"] = selectedRelease;
        context.Items["MiniAdmin.Gateway.CanaryDecision"] = decision;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Gateway-Release"] = selectedRelease;
            context.Response.Headers["X-Gateway-Canary-Reason"] = decision.Reason;
            return Task.CompletedTask;
        });

        return selected;
    }

    private string GetRelease(DestinationState destination)
    {
        var metadata = destination.Model.Config.Metadata;
        if (metadata is not null)
        {
            var match = metadata.FirstOrDefault(item =>
                item.Key.Equals(options.DestinationMetadataKey, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match.Value))
            {
                return match.Value;
            }
        }

        return options.StableDestinationValue;
    }

    private static int StableIndex(string routingKey, string destinationId)
    {
        return CanaryDecisionService.CalculateBucket($"{routingKey}:{destinationId}");
    }
}
