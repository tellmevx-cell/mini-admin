using Microsoft.Extensions.Options;
using MiniAdmin.Application.Contracts.Menus;
using MiniAdmin.Infrastructure.Caching;
using MiniAdmin.Platform.Caching;
using MiniAdmin.Platform.Navigation;

namespace MiniAdmin.Tests;

public sealed class LocalizationCacheTests
{
    [Fact]
    public async Task Menu_cache_keys_are_partitioned_by_supported_culture()
    {
        var platformCache = new LogicalKeyCapturingCache();
        var cache = new DistributedUserAuthorizationCache(
            platformCache,
            Options.Create(new CacheOptions()));
        static Task<IReadOnlyList<VbenMenuDto>> Factory(CancellationToken _) =>
            Task.FromResult<IReadOnlyList<VbenMenuDto>>([]);

        await cache.GetMenusAsync("Admin", "zh-CN", Factory);
        await cache.GetMenusAsync("Admin", "en-US", Factory);

        Assert.Equal(["admin:zh-cn", "admin:en-us"], platformCache.LogicalKeys);
    }

    [Theory]
    [InlineData("zh-CN", "中文")]
    [InlineData("en-US", "English")]
    [InlineData("en-GB", "English")]
    [InlineData(null, "中文")]
    public void Localized_text_resolves_supported_language(string? culture, string expected)
    {
        Assert.Equal(expected, new LocalizedText("中文", "English").Resolve(culture));
    }

    private sealed class LogicalKeyCapturingCache : IPlatformCache
    {
        public List<string> LogicalKeys { get; } = [];

        public async Task<T?> GetOrCreateAsync<T>(
            string category,
            string logicalKey,
            Guid? tenantId,
            IReadOnlyCollection<string> tags,
            Func<CancellationToken, Task<T?>> factory,
            TimeSpan? expiresAfter = null,
            CancellationToken cancellationToken = default)
        {
            LogicalKeys.Add(logicalKey);
            return await factory(cancellationToken);
        }

        public Task RemoveAsync(
            string category,
            string logicalKey,
            Guid? tenantId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task InvalidateTagsAsync(
            Guid? tenantId,
            IReadOnlyCollection<string> tags,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<PlatformCacheEntryInfo>> GetKnownEntriesAsync(
            string? category = null,
            Guid? tenantId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlatformCacheEntryInfo>>([]);
    }
}
