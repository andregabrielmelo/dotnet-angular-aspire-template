namespace AppTemplate.Core.Interfaces;

/// <summary>
/// Invalidates cached data by tag after a write. Implemented by <c>CacheInvalidator</c> in
/// Infrastructure (HybridCache), which also evicts every layer the host adds as an
/// <c>ICacheTagEvictor</c> (Web: the HTTP output cache), so use cases don't depend on HTTP.
/// Call it after the write has been saved (committed), never inside an open transaction:
/// invalidating first would let a concurrent read re-cache the old data.
/// Used by every user write handler.
/// </summary>
public interface ICacheInvalidator
{
    ValueTask InvalidateAsync(string tag, CancellationToken cancellationToken);
}
