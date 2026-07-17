using Microsoft.Extensions.Caching.Distributed;
using MiniAdmin.Application.Contracts.Caching;

namespace MiniAdmin.Infrastructure.Caching;

public sealed class DistributedCacheHealthProbe(IDistributedCache cache) : IPrimaryCacheHealthProbe
{
    public async Task ProbeAsync(CancellationToken cancellationToken = default)
    {
        var key = $"mini-admin:health:{Guid.NewGuid():N}";
        var expected = Guid.NewGuid().ToByteArray();
        try
        {
            await cache.SetAsync(
                key,
                expected,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
                },
                cancellationToken);
            var actual = await cache.GetAsync(key, cancellationToken);
            if (actual is null || !actual.AsSpan().SequenceEqual(expected))
            {
                throw new InvalidOperationException("Distributed cache read/write probe did not round-trip.");
            }
        }
        finally
        {
            try
            {
                await cache.RemoveAsync(key, cancellationToken);
            }
            catch
            {
                // The health key expires automatically.
            }
        }
    }
}
