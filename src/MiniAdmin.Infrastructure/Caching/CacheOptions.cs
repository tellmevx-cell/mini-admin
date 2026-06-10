namespace MiniAdmin.Infrastructure.Caching;

public sealed class CacheOptions
{
    public string Provider { get; set; } = "Memory";

    public string KeyPrefix { get; set; } = "mini-admin:";

    public int DefaultExpireMinutes { get; set; } = 30;

    public int SecurityStampExpireMinutes { get; set; } = 5;

    public int PermissionExpireMinutes { get; set; } = 30;

    public int MenuExpireMinutes { get; set; } = 30;

    public RedisCacheOptions Redis { get; set; } = new();
}

public sealed class RedisCacheOptions
{
    public string Configuration { get; set; } = string.Empty;
}
