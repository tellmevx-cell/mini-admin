namespace MiniAdmin.Api.RateLimiting;

public sealed class MiniAdminRateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; init; } = true;

    public int PermitLimit { get; init; } = 600;

    public int WindowSeconds { get; init; } = 60;

    public int QueueLimit { get; init; }

    public int LoginPermitLimit { get; init; } = 10;

    public int LoginWindowSeconds { get; init; } = 60;

    public int LoginQueueLimit { get; init; }

    public int UploadPermitLimit { get; init; } = 4;

    public int UploadQueueLimit { get; init; }
}
