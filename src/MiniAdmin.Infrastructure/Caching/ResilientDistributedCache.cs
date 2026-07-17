using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using MiniAdmin.Application.Contracts.Caching;

namespace MiniAdmin.Infrastructure.Caching;

public sealed class ResilientDistributedCache(
    IDistributedCache primary,
    IDistributedCache fallback,
    ILogger<ResilientDistributedCache> logger) : IDistributedCache, IPrimaryCacheHealthProbe
{
    private static readonly TimeSpan PrimaryFailureBackoff = TimeSpan.FromSeconds(30);
    private DateTimeOffset _primaryUnavailableUntil;

    public byte[]? Get(string key)
    {
        if (IsPrimaryUnavailable())
        {
            return fallback.Get(key);
        }

        try
        {
            return primary.Get(key) ?? fallback.Get(key);
        }
        catch (Exception ex)
        {
            MarkPrimaryUnavailable();
            logger.LogWarning(ex, "Primary distributed cache get failed, using memory fallback.");
            return fallback.Get(key);
        }
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        if (IsPrimaryUnavailable())
        {
            return await fallback.GetAsync(key, token);
        }

        try
        {
            return await primary.GetAsync(key, token) ??
                await fallback.GetAsync(key, token);
        }
        catch (Exception ex)
        {
            MarkPrimaryUnavailable();
            logger.LogWarning(ex, "Primary distributed cache get failed, using memory fallback.");
            return await fallback.GetAsync(key, token);
        }
    }

    public void Refresh(string key)
    {
        if (!IsPrimaryUnavailable())
        {
            try
            {
                primary.Refresh(key);
            }
            catch (Exception ex)
            {
                MarkPrimaryUnavailable();
                logger.LogWarning(ex, "Primary distributed cache refresh failed.");
            }
        }

        fallback.Refresh(key);
    }

    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        if (!IsPrimaryUnavailable())
        {
            try
            {
                await primary.RefreshAsync(key, token);
            }
            catch (Exception ex)
            {
                MarkPrimaryUnavailable();
                logger.LogWarning(ex, "Primary distributed cache refresh failed.");
            }
        }

        await fallback.RefreshAsync(key, token);
    }

    public void Remove(string key)
    {
        if (!IsPrimaryUnavailable())
        {
            try
            {
                primary.Remove(key);
            }
            catch (Exception ex)
            {
                MarkPrimaryUnavailable();
                logger.LogWarning(ex, "Primary distributed cache remove failed.");
            }
        }

        fallback.Remove(key);
    }

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        if (!IsPrimaryUnavailable())
        {
            try
            {
                await primary.RemoveAsync(key, token);
            }
            catch (Exception ex)
            {
                MarkPrimaryUnavailable();
                logger.LogWarning(ex, "Primary distributed cache remove failed.");
            }
        }

        await fallback.RemoveAsync(key, token);
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        if (!IsPrimaryUnavailable())
        {
            try
            {
                primary.Set(key, value, options);
            }
            catch (Exception ex)
            {
                MarkPrimaryUnavailable();
                logger.LogWarning(ex, "Primary distributed cache set failed, writing memory fallback.");
            }
        }

        fallback.Set(key, value, options);
    }

    public async Task SetAsync(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        if (!IsPrimaryUnavailable())
        {
            try
            {
                await primary.SetAsync(key, value, options, token);
            }
            catch (Exception ex)
            {
                MarkPrimaryUnavailable();
                logger.LogWarning(ex, "Primary distributed cache set failed, writing memory fallback.");
            }
        }

        await fallback.SetAsync(key, value, options, token);
    }

    public async Task ProbeAsync(CancellationToken cancellationToken = default)
    {
        var key = $"mini-admin:health:{Guid.NewGuid():N}";
        var expected = Guid.NewGuid().ToByteArray();
        try
        {
            await primary.SetAsync(
                key,
                expected,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
                },
                cancellationToken);
            var actual = await primary.GetAsync(key, cancellationToken);
            if (actual is null || !actual.AsSpan().SequenceEqual(expected))
            {
                throw new InvalidOperationException("Primary distributed cache read/write probe did not round-trip.");
            }
        }
        finally
        {
            try
            {
                await primary.RemoveAsync(key, cancellationToken);
            }
            catch
            {
                // Preserve the original probe failure; the key has a short expiration.
            }
        }
    }

    private bool IsPrimaryUnavailable()
    {
        return DateTimeOffset.UtcNow < _primaryUnavailableUntil;
    }

    private void MarkPrimaryUnavailable()
    {
        _primaryUnavailableUntil = DateTimeOffset.UtcNow.Add(PrimaryFailureBackoff);
    }
}
