using MiniAdmin.Platform.Caching;

namespace MiniAdmin.Tests;

internal sealed class TestPlatformCache : IPlatformCache
{
    public Task<T?> GetOrCreateAsync<T>(
        string category,
        string logicalKey,
        Guid? tenantId,
        IReadOnlyCollection<string> tags,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan? expiresAfter = null,
        CancellationToken cancellationToken = default)
    {
        return factory(cancellationToken);
    }

    public Task RemoveAsync(
        string category,
        string logicalKey,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task InvalidateTagsAsync(
        Guid? tenantId,
        IReadOnlyCollection<string> tags,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PlatformCacheEntryInfo>> GetKnownEntriesAsync(
        string? category = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<PlatformCacheEntryInfo>>([]);
    }
}
