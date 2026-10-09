using Microsoft.Extensions.Caching.Hybrid;

namespace AppTemplate.UseCases.Caching;

public static class HybridCacheExtensions
{
    /// <summary>
    /// <see cref="HybridCache.GetOrCreateAsync{TState, T}"/> for lookups that can miss: a
    /// <c>null</c> result is returned but not cached, so "not found" always reaches the factory.
    /// Otherwise any caller could fill the cache with entries for ids that don't exist, and a
    /// newly created row would stay invisible until the cached miss expired.
    /// </summary>
    public static async ValueTask<T?> GetOrCreateExistingAsync<TState, T>(
        this HybridCache cache,
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T?>> factory,
        HybridCacheEntryOptions? options,
        IEnumerable<string>? tags,
        CancellationToken cancellationToken
    )
        where T : class
    {
        try
        {
            return await cache.GetOrCreateAsync(
                key,
                (state, factory),
                static async (s, token) =>
                    await s.factory(s.state, token) ?? throw new MissNotCachedException(),
                options,
                tags,
                cancellationToken
            );
        }
        catch (MissNotCachedException)
        {
            // HybridCache stores nothing when the factory throws, and every concurrent caller
            // joined to this fetch gets the same exception, so stampede protection still holds.
            return null;
        }
    }

    private sealed class MissNotCachedException : Exception;
}
