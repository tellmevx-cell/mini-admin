using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Platform.Caching;
using MiniAdmin.Platform.DynamicApi;

namespace MiniAdmin.Application.Platform;

[DynamicApi("platform/cache", Name = "PlatformCache", Tag = "平台缓存")]
public sealed class PlatformCacheAppService(
    IPlatformCache platformCache,
    ICurrentTenant currentTenant)
{
    [DynamicGet(
        "entries",
        Permission = "platform:cache:query",
        Resource = "platform.cache",
        Action = "query",
        OperationId = "GetPlatformCacheEntries",
        Summary = "查询当前节点已知缓存键")]
    public Task<IReadOnlyList<PlatformCacheEntryInfo>> GetEntriesAsync(
        [DynamicApiParameter(DynamicApiParameterSource.Query)] string? category = null,
        [DynamicApiParameter(DynamicApiParameterSource.Query)] Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        return platformCache.GetKnownEntriesAsync(
            category,
            ResolveTenantId(tenantId),
            cancellationToken);
    }

    [DynamicPost(
        "invalidate-tags",
        Permission = "platform:cache:clear",
        Resource = "platform.cache",
        Action = "clear",
        OperationId = "InvalidatePlatformCacheTags",
        Summary = "按标签精准失效缓存")]
    public async Task<PlatformCacheOperationResult> InvalidateTagsAsync(
        PlatformCacheTagInvalidationRequest request,
        CancellationToken cancellationToken = default)
    {
        var tags = request.Tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (tags.Length == 0)
        {
            throw new InvalidOperationException("至少需要提供一个缓存标签。");
        }

        await platformCache.InvalidateTagsAsync(
            ResolveTenantId(request.TenantId),
            tags,
            cancellationToken);
        return new PlatformCacheOperationResult(true, $"已失效 {tags.Length} 个标签。");
    }

    [DynamicPost(
        "remove-entry",
        Permission = "platform:cache:clear",
        Resource = "platform.cache",
        Action = "clear",
        OperationId = "RemovePlatformCacheEntry",
        Summary = "按逻辑键移除缓存")]
    public async Task<PlatformCacheOperationResult> RemoveEntryAsync(
        PlatformCacheEntryRemovalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Category) ||
            string.IsNullOrWhiteSpace(request.LogicalKey))
        {
            throw new InvalidOperationException("缓存分类和逻辑键不能为空。");
        }

        await platformCache.RemoveAsync(
            request.Category.Trim(),
            request.LogicalKey.Trim(),
            ResolveTenantId(request.TenantId),
            cancellationToken);
        return new PlatformCacheOperationResult(true, "缓存逻辑键已失效。");
    }

    private Guid? ResolveTenantId(Guid? requestedTenantId)
    {
        return currentTenant.TenantId ?? requestedTenantId;
    }
}

public sealed record PlatformCacheTagInvalidationRequest(
    Guid? TenantId,
    IReadOnlyList<string> Tags);

public sealed record PlatformCacheEntryRemovalRequest(
    Guid? TenantId,
    string Category,
    string LogicalKey);

public sealed record PlatformCacheOperationResult(bool Success, string Message);
