namespace AppTemplate.Infrastructure.Caching;

/// <summary>
/// Another cache layer, owned by the host, that must drop a tag whenever use-case data is
/// invalidated. Web registers one for the HTTP output cache. <see cref="CacheInvalidator"/>
/// calls every registered evictor after HybridCache, each best-effort.
/// </summary>
public interface ICacheTagEvictor
{
    /// <summary>A short name for logs, such as "output cache".</summary>
    string Layer { get; }

    ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken);
}
