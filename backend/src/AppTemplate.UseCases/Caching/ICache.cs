namespace AppTemplate.UseCases.Caching;

/// <summary>
/// Application-level cache. Implemented in Infrastructure (HybridCache over Redis), so UseCases
/// decides what to cache and when to invalidate without knowing the caching technology.
/// Expirations are centralized defaults in Infrastructure - see ADR 007.
/// </summary>
public interface ICache
{
    ValueTask<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Removes the entry from the distributed cache and this instance's local cache. Other
    /// instances keep their local copy until it expires.
    /// </summary>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
}
