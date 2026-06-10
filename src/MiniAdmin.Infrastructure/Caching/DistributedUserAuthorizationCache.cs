using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using MiniAdmin.Application.Contracts.Caching;
using MiniAdmin.Application.Contracts.Menus;

namespace MiniAdmin.Infrastructure.Caching;

public sealed class DistributedUserAuthorizationCache(
    IDistributedCache cache,
    IOptions<CacheOptions> options) : IUserAuthorizationCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CacheOptions _options = options.Value;

    public Task<string?> GetSecurityStampAsync(
        Guid userId,
        Func<CancellationToken, Task<string?>> factory,
        CancellationToken cancellationToken = default)
    {
        return GetOrCreateStringAsync(
            SecurityStampKey(userId),
            factory,
            TimeSpan.FromMinutes(Math.Max(_options.SecurityStampExpireMinutes, 1)),
            cancellationToken);
    }

    public Task<IReadOnlyList<string>> GetPermissionCodesAsync(
        string userName,
        Func<CancellationToken, Task<IReadOnlyList<string>>> factory,
        CancellationToken cancellationToken = default)
    {
        return GetOrCreateJsonAsync(
            PermissionCodesKey(userName),
            factory,
            TimeSpan.FromMinutes(Math.Max(_options.PermissionExpireMinutes, 1)),
            cancellationToken);
    }

    public Task<IReadOnlyList<VbenMenuDto>> GetMenusAsync(
        string userName,
        Func<CancellationToken, Task<IReadOnlyList<VbenMenuDto>>> factory,
        CancellationToken cancellationToken = default)
    {
        return GetOrCreateJsonAsync(
            MenusKey(userName),
            factory,
            TimeSpan.FromMinutes(Math.Max(_options.MenuExpireMinutes, 1)),
            cancellationToken);
    }

    public async Task RemoveUserAsync(
        Guid userId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync(SecurityStampKey(userId), cancellationToken);
        await cache.RemoveAsync(PermissionCodesKey(userName), cancellationToken);
        await cache.RemoveAsync(MenusKey(userName), cancellationToken);
    }

    private async Task<string?> GetOrCreateStringAsync(
        string key,
        Func<CancellationToken, Task<string?>> factory,
        TimeSpan expiresAfter,
        CancellationToken cancellationToken)
    {
        var cached = await cache.GetStringAsync(key, cancellationToken);
        if (cached is not null)
        {
            return cached.Length == 0 ? null : cached;
        }

        var value = await factory(cancellationToken);
        await cache.SetStringAsync(
            key,
            value ?? string.Empty,
            CreateEntryOptions(expiresAfter),
            cancellationToken);

        return value;
    }

    private async Task<IReadOnlyList<T>> GetOrCreateJsonAsync<T>(
        string key,
        Func<CancellationToken, Task<IReadOnlyList<T>>> factory,
        TimeSpan expiresAfter,
        CancellationToken cancellationToken)
    {
        var cached = await cache.GetStringAsync(key, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            return JsonSerializer.Deserialize<T[]>(cached, JsonOptions) ?? [];
        }

        var value = await factory(cancellationToken);
        await cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(value, JsonOptions),
            CreateEntryOptions(expiresAfter),
            cancellationToken);

        return value;
    }

    private DistributedCacheEntryOptions CreateEntryOptions(TimeSpan expiresAfter)
    {
        var defaultExpireMinutes = Math.Max(_options.DefaultExpireMinutes, 1);
        var absoluteExpiration = expiresAfter <= TimeSpan.Zero
            ? TimeSpan.FromMinutes(defaultExpireMinutes)
            : expiresAfter;

        return new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpiration
        };
    }

    private string SecurityStampKey(Guid userId)
    {
        return $"{NormalizePrefix()}auth:security-stamp:{userId:N}";
    }

    private string PermissionCodesKey(string userName)
    {
        return $"{NormalizePrefix()}auth:permissions:{NormalizeUserName(userName)}";
    }

    private string MenusKey(string userName)
    {
        return $"{NormalizePrefix()}auth:menus:{NormalizeUserName(userName)}";
    }

    private string NormalizePrefix()
    {
        return string.IsNullOrWhiteSpace(_options.KeyPrefix) ? "mini-admin:" : _options.KeyPrefix.Trim();
    }

    private static string NormalizeUserName(string userName)
    {
        return userName.Trim().ToLowerInvariant();
    }
}
