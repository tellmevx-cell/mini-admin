namespace MiniAdmin.Infrastructure.Persistence;

public sealed class OnlineUserOptions
{
    public int ActiveTimeoutMinutes { get; set; } = 30;

    public int TouchThrottleSeconds { get; set; } = 60;
}
