using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniAdmin.Platform.Caching;

namespace MiniAdmin.Infrastructure.Caching;

public sealed class PlatformCache(
    IDistributedCache cache,
    IOptions<CacheOptions> options,
    ILogger<PlatformCache> logger) : IPlatformCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> keyLocks = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PlatformCacheEntryInfo> catalog = new(StringComparer.Ordinal);
    private readonly CacheOptions cacheOptions = options.Value;

    public async Task<T?> GetOrCreateAsync<T>(
        string category,
        string logicalKey,
        Guid? tenantId,
        IReadOnlyCollection<string> tags,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan? expiresAfter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalKey);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(factory);

        var normalizedCategory = NormalizeText(category);
        var normalizedTags = tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(NormalizeText)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var physicalKey = await CreatePhysicalKeyAsync(
            normalizedCategory,
            logicalKey,
            tenantId,
            normalizedTags,
            cancellationToken);
        var cached = await TryReadAsync<T>(physicalKey, cancellationToken);
        if (cached.Found)
        {
            TouchCatalog(physicalKey);
            return cached.Value;
        }

        var lockKey = CreateLockKey(normalizedCategory, logicalKey, tenantId);
        var keyLock = keyLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        await keyLock.WaitAsync(cancellationToken);
        try
        {
            physicalKey = await CreatePhysicalKeyAsync(
                normalizedCategory,
                logicalKey,
                tenantId,
                normalizedTags,
                cancellationToken);
            cached = await TryReadAsync<T>(physicalKey, cancellationToken);
            if (cached.Found)
            {
                TouchCatalog(physicalKey);
                return cached.Value;
            }

            var value = await factory(cancellationToken);
            var duration = expiresAfter.GetValueOrDefault(
                TimeSpan.FromMinutes(Math.Max(cacheOptions.DefaultExpireMinutes, 1)));
            if (duration <= TimeSpan.Zero)
            {
                duration = TimeSpan.FromMinutes(Math.Max(cacheOptions.DefaultExpireMinutes, 1));
            }

            var expiresAt = DateTimeOffset.UtcNow.Add(duration);
            var stored = await TryWriteAsync(
                physicalKey,
                JsonSerializer.Serialize(new CacheEnvelope<T>(value), JsonOptions),
                duration,
                cancellationToken);
            if (stored)
            {
                RecordCatalogEntry(new PlatformCacheEntryInfo(
                    logicalKey,
                    physicalKey,
                    normalizedCategory,
                    tenantId,
                    normalizedTags,
                    DateTimeOffset.UtcNow,
                    expiresAt));
            }

            return value;
        }
        finally
        {
            keyLock.Release();
        }
    }

    public async Task RemoveAsync(
        string category,
        string logicalKey,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalKey);

        await WriteGateAsync(
            CreateLogicalGateKey(NormalizeText(category), logicalKey, tenantId),
            cancellationToken);
        await RemoveCatalogEntriesAsync(
            entry => entry.TenantId == tenantId &&
                entry.Category.Equals(NormalizeText(category), StringComparison.Ordinal) &&
                entry.LogicalKey.Equals(logicalKey, StringComparison.Ordinal),
            cancellationToken);
    }

    public async Task InvalidateTagsAsync(
        Guid? tenantId,
        IReadOnlyCollection<string> tags,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tags);
        var normalizedTags = tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(NormalizeText)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        if (normalizedTags.Count == 0)
        {
            return;
        }

        foreach (var tag in normalizedTags)
        {
            await WriteGateAsync(CreateTagGateKey(tag, tenantId), cancellationToken);
        }

        await RemoveCatalogEntriesAsync(
            entry => (!tenantId.HasValue || entry.TenantId == tenantId) &&
                entry.Tags.Any(normalizedTags.Contains),
            cancellationToken);
    }

    public Task<IReadOnlyList<PlatformCacheEntryInfo>> GetKnownEntriesAsync(
        string? category = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedCategory = string.IsNullOrWhiteSpace(category)
            ? null
            : NormalizeText(category);
        IReadOnlyList<PlatformCacheEntryInfo> entries = catalog.Values
            .Where(entry => normalizedCategory is null ||
                entry.Category.Equals(normalizedCategory, StringComparison.Ordinal))
            .Where(entry => !tenantId.HasValue || entry.TenantId == tenantId)
            .Where(entry => !entry.ExpiresAt.HasValue || entry.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderBy(entry => entry.Category, StringComparer.Ordinal)
            .ThenBy(entry => entry.LogicalKey, StringComparer.Ordinal)
            .ToArray();
        return Task.FromResult(entries);
    }

    private async Task<string> CreatePhysicalKeyAsync(
        string category,
        string logicalKey,
        Guid? tenantId,
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken)
    {
        var versions = new List<string>
        {
            await ReadGateAsync(CreateLogicalGateKey(category, logicalKey, tenantId), cancellationToken)
        };

        foreach (var tag in tags)
        {
            versions.Add(await ReadGateAsync(CreateTagGateKey(tag, null), cancellationToken));
            if (tenantId.HasValue)
            {
                versions.Add(await ReadGateAsync(CreateTagGateKey(tag, tenantId), cancellationToken));
            }
        }

        var material = string.Join('|', category, logicalKey, TenantScope(tenantId),
            string.Join(',', tags), string.Join(',', versions));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)))
            .ToLowerInvariant();
        return $"{NormalizePrefix()}cache:data:{category}:{TenantScope(tenantId)}:{hash[..40]}";
    }

    private async Task<(bool Found, T? Value)> TryReadAsync<T>(
        string physicalKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await cache.GetStringAsync(physicalKey, cancellationToken);
            if (json is null)
            {
                return (false, default);
            }

            var envelope = JsonSerializer.Deserialize<CacheEnvelope<T>>(json, JsonOptions);
            return envelope is null ? (false, default) : (true, envelope.Value);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "读取平台缓存 {CacheKey} 失败，已回退数据源。", physicalKey);
            return (false, default);
        }
    }

    private async Task<bool> TryWriteAsync(
        string physicalKey,
        string json,
        TimeSpan expiresAfter,
        CancellationToken cancellationToken)
    {
        try
        {
            await cache.SetStringAsync(
                physicalKey,
                json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiresAfter
                },
                cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "写入平台缓存 {CacheKey} 失败，本次结果不缓存。", physicalKey);
            return false;
        }
    }

    private async Task<string> ReadGateAsync(string gateKey, CancellationToken cancellationToken)
    {
        try
        {
            return await cache.GetStringAsync(gateKey, cancellationToken) ?? "0";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "读取缓存版本门控 {GateKey} 失败。", gateKey);
            return "unavailable";
        }
    }

    private async Task WriteGateAsync(string gateKey, CancellationToken cancellationToken)
    {
        try
        {
            await cache.SetStringAsync(
                gateKey,
                Guid.NewGuid().ToString("N"),
                new DistributedCacheEntryOptions(),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "更新缓存版本门控 {GateKey} 失败。", gateKey);
        }
    }

    private async Task RemoveCatalogEntriesAsync(
        Func<PlatformCacheEntryInfo, bool> predicate,
        CancellationToken cancellationToken)
    {
        var entries = catalog.Values.Where(predicate).ToArray();
        foreach (var entry in entries)
        {
            catalog.TryRemove(entry.PhysicalKey, out _);
            try
            {
                await cache.RemoveAsync(entry.PhysicalKey, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "清理旧缓存实体 {CacheKey} 失败，将等待自然过期。", entry.PhysicalKey);
            }
        }
    }

    private void RecordCatalogEntry(PlatformCacheEntryInfo entry)
    {
        foreach (var existing in catalog.Values.Where(existing =>
                     existing.TenantId == entry.TenantId &&
                     existing.Category.Equals(entry.Category, StringComparison.Ordinal) &&
                     existing.LogicalKey.Equals(entry.LogicalKey, StringComparison.Ordinal) &&
                     !existing.PhysicalKey.Equals(entry.PhysicalKey, StringComparison.Ordinal)))
        {
            catalog.TryRemove(existing.PhysicalKey, out _);
        }

        catalog[entry.PhysicalKey] = entry;
    }

    private void TouchCatalog(string physicalKey)
    {
        if (catalog.TryGetValue(physicalKey, out var entry))
        {
            catalog[physicalKey] = entry with { LastAccessedAt = DateTimeOffset.UtcNow };
        }
    }

    private string CreateLogicalGateKey(string category, string logicalKey, Guid? tenantId)
    {
        return $"{NormalizePrefix()}cache:gate:key:{category}:{TenantScope(tenantId)}:{Hash(logicalKey)}";
    }

    private string CreateTagGateKey(string tag, Guid? tenantId)
    {
        return $"{NormalizePrefix()}cache:gate:tag:{TenantScope(tenantId)}:{Hash(tag)}";
    }

    private static string CreateLockKey(string category, string logicalKey, Guid? tenantId)
    {
        return $"{category}:{TenantScope(tenantId)}:{Hash(logicalKey)}";
    }

    private string NormalizePrefix()
    {
        var prefix = string.IsNullOrWhiteSpace(cacheOptions.KeyPrefix)
            ? "mini-admin:"
            : cacheOptions.KeyPrefix.Trim();
        return prefix.EndsWith(':') ? prefix : $"{prefix}:";
    }

    private static string TenantScope(Guid? tenantId)
    {
        return tenantId?.ToString("N") ?? "global";
    }

    private static string NormalizeText(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant()[..24];
    }

    private sealed record CacheEnvelope<T>(T? Value);
}
