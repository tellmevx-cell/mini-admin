namespace MiniAdmin.Gateway;

public sealed class GatewayRateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    public int PermitLimit { get; set; } = 1200;

    public int WindowSeconds { get; set; } = 60;

    public int QueueLimit { get; set; }

    public int LoginPermitLimit { get; set; } = 20;

    public int LoginWindowSeconds { get; set; } = 60;

    public int LoginQueueLimit { get; set; }
}
