using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MiniAdmin.Infrastructure.Caching;

namespace MiniAdmin.Tests;

public sealed class ResilientDistributedCacheTests
{
    [Fact]
    public async Task Uses_Memory_Fallback_When_Primary_Cache_Fails()
    {
        var fallback = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var cache = new ResilientDistributedCache(
            new ThrowingDistributedCache(),
            fallback,
            NullLogger<ResilientDistributedCache>.Instance);

        await cache.SetStringAsync(
            "tenant-list-test",
            "ok",
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
            });

        var value = await cache.GetStringAsync("tenant-list-test");

        Assert.Equal("ok", value);
    }

    [Fact]
    public async Task Skips_Primary_Cache_Temporarily_After_Failure()
    {
        var primary = new CountingThrowingDistributedCache();
        var fallback = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var cache = new ResilientDistributedCache(
            primary,
            fallback,
            NullLogger<ResilientDistributedCache>.Instance);

        await cache.SetStringAsync(
            "redis-circuit-test",
            "ok",
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
            });
        var value = await cache.GetStringAsync("redis-circuit-test");

        Assert.Equal("ok", value);
        Assert.Equal(1, primary.CallCount);
    }

    private sealed class CountingThrowingDistributedCache : ThrowingDistributedCache
    {
        public int CallCount { get; private set; }

        public override byte[]? Get(string key)
        {
            CallCount++;
            return base.Get(key);
        }

        public override Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            CallCount++;
            return base.GetAsync(key, token);
        }

        public override void Refresh(string key)
        {
            CallCount++;
            base.Refresh(key);
        }

        public override Task RefreshAsync(string key, CancellationToken token = default)
        {
            CallCount++;
            return base.RefreshAsync(key, token);
        }

        public override void Remove(string key)
        {
            CallCount++;
            base.Remove(key);
        }

        public override Task RemoveAsync(string key, CancellationToken token = default)
        {
            CallCount++;
            return base.RemoveAsync(key, token);
        }

        public override void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            CallCount++;
            base.Set(key, value, options);
        }

        public override Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
            CancellationToken token = default)
        {
            CallCount++;
            return base.SetAsync(key, value, options, token);
        }
    }

    private class ThrowingDistributedCache : IDistributedCache
    {
        public virtual byte[]? Get(string key)
        {
            throw CreateException();
        }

        public virtual Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            throw CreateException();
        }

        public virtual void Refresh(string key)
        {
            throw CreateException();
        }

        public virtual Task RefreshAsync(string key, CancellationToken token = default)
        {
            throw CreateException();
        }

        public virtual void Remove(string key)
        {
            throw CreateException();
        }

        public virtual Task RemoveAsync(string key, CancellationToken token = default)
        {
            throw CreateException();
        }

        public virtual void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            throw CreateException();
        }

        public virtual Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
            CancellationToken token = default)
        {
            throw CreateException();
        }

        private static Exception CreateException()
        {
            return new InvalidOperationException("Primary cache unavailable.");
        }
    }
}
