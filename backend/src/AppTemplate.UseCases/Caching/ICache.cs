namespace AppTemplate.UseCases.Caching;

/// <summary>
/// Application-level cache. Implemented in Infrastructure (HybridCache over Redis), so UseCases
/// decides what to cache and when to invalidate without knowing the caching technology.
/// Expirations are centralized defaults in Infrastructure - see ADR 007.
/// </summary>
public interface ICache
{
    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, or runs <paramref name="factory"/> and
    /// caches its result. A <c>null</c> result is returned but not cached, so misses (e.g. not
    /// found) always reach the factory. Concurrent callers for the same key share one factory call.
    /// </summary>
    ValueTask<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Removes the entry from the distributed cache and this instance's local cache. Other
    /// instances keep their local copy until it expires. Best-effort: a distributed cache failure
    /// is logged, not thrown, so it can't fail a write that already succeeded.
    /// </summary>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
}
