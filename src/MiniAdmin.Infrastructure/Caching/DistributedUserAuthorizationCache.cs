using Microsoft.Extensions.Options;
using MiniAdmin.Application.Contracts.Caching;
using MiniAdmin.Application.Contracts.Menus;
using MiniAdmin.Platform.Caching;

namespace MiniAdmin.Infrastructure.Caching;

public sealed class DistributedUserAuthorizationCache(
    IPlatformCache cache,
    IOptions<CacheOptions> options) : IUserAuthorizationCache
{
    private readonly CacheOptions _options = options.Value;

    public Task<string?> GetSecurityStampAsync(
        Guid userId,
        Func<CancellationToken, Task<string?>> factory,
        CancellationToken cancellationToken = default)
    {
        return cache.GetOrCreateAsync(
            "authorization-stamp",
            userId.ToString("N"),
            tenantId: null,
            [UserIdTag(userId)],
            factory,
            TimeSpan.FromMinutes(Math.Max(_options.SecurityStampExpireMinutes, 1)),
            cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetPermissionCodesAsync(
        string userName,
        Func<CancellationToken, Task<IReadOnlyList<string>>> factory,
        CancellationToken cancellationToken = default)
    {
        return await cache.GetOrCreateAsync<IReadOnlyList<string>>(
            "authorization-permissions",
            NormalizeUserName(userName),
            tenantId: null,
            [UserNameTag(userName), "authorization-permissions"],
            async token => await factory(token),
            TimeSpan.FromMinutes(Math.Max(_options.PermissionExpireMinutes, 1)),
            cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<VbenMenuDto>> GetMenusAsync(
        string userName,
        string cultureName,
        Func<CancellationToken, Task<IReadOnlyList<VbenMenuDto>>> factory,
        CancellationToken cancellationToken = default)
    {
        return await cache.GetOrCreateAsync<IReadOnlyList<VbenMenuDto>>(
            "authorization-menus",
            $"{NormalizeUserName(userName)}:{NormalizeCulture(cultureName)}",
            tenantId: null,
            [UserNameTag(userName), "authorization-menus"],
            async token => await factory(token),
            TimeSpan.FromMinutes(Math.Max(_options.MenuExpireMinutes, 1)),
            cancellationToken) ?? [];
    }

    public async Task RemoveUserAsync(
        Guid userId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        await cache.InvalidateTagsAsync(
            tenantId: null,
            [UserIdTag(userId), UserNameTag(userName)],
            cancellationToken);
    }

    private static string NormalizeUserName(string userName)
    {
        return userName.Trim().ToLowerInvariant();
    }

    private static string NormalizeCulture(string cultureName)
    {
        return cultureName.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? "en-us"
            : "zh-cn";
    }

    private static string UserIdTag(Guid userId) => $"authorization-user-id:{userId:N}";

    private static string UserNameTag(string userName) =>
        $"authorization-user-name:{NormalizeUserName(userName)}";
}
