namespace MiniAdmin.Gateway;

public sealed class GatewayCanaryOptions
{
    public const string SectionName = "Canary";

    public bool Enabled { get; set; }

    public int Percentage { get; set; }

    public string ReleaseHeaderName { get; set; } = "X-Release-Channel";

    public string CanaryHeaderValue { get; set; } = "canary";

    public string StableHeaderValue { get; set; } = "stable";

    public string TenantHeaderName { get; set; } = "X-Tenant-Id";

    public string UserHeaderName { get; set; } = "X-User-Id";

    public string RoutingKeyHeaderName { get; set; } = "X-Routing-Key";

    public string DestinationMetadataKey { get; set; } = "Release";

    public string CanaryDestinationValue { get; set; } = "canary";

    public string StableDestinationValue { get; set; } = "stable";

    public string[] TenantWhitelist { get; set; } = [];

    public string[] UserWhitelist { get; set; } = [];

    public string[] IpWhitelist { get; set; } = [];

    public Dictionary<string, string> HeaderMatches { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed record CanaryDecision(
    bool UseCanary,
    string Reason,
    string RoutingKey);
