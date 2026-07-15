namespace MiniAdmin.Platform.Caching;

public sealed record PlatformCacheEntryInfo(
    string LogicalKey,
    string PhysicalKey,
    string Category,
    Guid? TenantId,
    IReadOnlyList<string> Tags,
    DateTimeOffset LastAccessedAt,
    DateTimeOffset? ExpiresAt);

public interface IPlatformCache
{
    Task<T?> GetOrCreateAsync<T>(
        string category,
        string logicalKey,
        Guid? tenantId,
        IReadOnlyCollection<string> tags,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan? expiresAfter = null,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        string category,
        string logicalKey,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    Task InvalidateTagsAsync(
        Guid? tenantId,
        IReadOnlyCollection<string> tags,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlatformCacheEntryInfo>> GetKnownEntriesAsync(
        string? category = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);
}
