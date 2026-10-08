using AppTemplate.UseCases.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace AppTemplate.Infrastructure.Caching;

internal sealed class HybridCacheService(HybridCache cache, ILogger<HybridCacheService> logger)
    : ICache
{
    public async ValueTask<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await cache.GetOrCreateAsync(
                key,
                factory,
                static async (factory, ct) =>
                {
                    var value = await factory(ct);
                    return value is null ? throw new MissNotCachedException() : value;
                },
                cancellationToken: cancellationToken
            );
        }
        catch (MissNotCachedException)
        {
            // HybridCache stores nothing when the factory throws, and every concurrent caller
            // joined to this fetch gets the same exception. Side effect: HybridCache's
            // UnderlyingDataQueryFailed EventSource counter also counts misses.
            return default!;
        }
    }

    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            // Clears this instance's L1 before calling L2, so only the Redis call can fail
            await cache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The write already succeeded; the stale L2 entry lives until it expires
            logger.LogWarning(ex, "Failed to remove cache entry {CacheKey}", key);
        }
    }

    private sealed class MissNotCachedException : Exception;
}
