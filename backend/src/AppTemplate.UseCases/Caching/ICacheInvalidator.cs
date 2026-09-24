namespace AppTemplate.UseCases.Caching;

/// <summary>
/// Invalidates cached data by tag after a write. Implemented in Web, where it covers both
/// HybridCache and the HTTP output cache, so use cases don't depend on HTTP concerns.
/// </summary>
public interface ICacheInvalidator
{
    ValueTask InvalidateAsync(string tag, CancellationToken cancellationToken);
}
