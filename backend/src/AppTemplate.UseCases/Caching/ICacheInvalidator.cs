namespace AppTemplate.UseCases.Caching;

/// <summary>
/// Invalidates cached data by tag after a write. Implemented in Web, where it covers both
/// HybridCache and the HTTP output cache, so use cases don't depend on HTTP concerns.
/// Call it after the write has been saved (committed), never inside an open transaction:
/// invalidating first would let a concurrent read re-cache the old data.
/// </summary>
public interface ICacheInvalidator
{
    ValueTask InvalidateAsync(string tag, CancellationToken cancellationToken);
}
