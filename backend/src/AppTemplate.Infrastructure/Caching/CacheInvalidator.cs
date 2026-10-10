using Microsoft.Extensions.Caching.Hybrid;

namespace AppTemplate.Infrastructure.Caching;

/// <summary>
/// Invalidates a tag in HybridCache (use-case data), then in every other layer the host
/// registered as an <see cref="ICacheTagEvictor"/> (Web: the HTTP output cache). With Redis as
/// L2 the invalidation reaches every API instance. Best-effort: callers invoke it after their
/// write has committed, so a cache failure (Redis down) is logged instead of thrown, and stale
/// entries live until they expire.
/// </summary>
internal sealed class CacheInvalidator(
    HybridCache hybridCache,
    IEnumerable<ICacheTagEvictor> otherLayers,
    ILogger<CacheInvalidator> logger
) : ICacheInvalidator
{
    public async ValueTask InvalidateAsync(string tag, CancellationToken cancellationToken)
    {
        try
        {
            await hybridCache.RemoveByTagAsync(tag, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to invalidate cache tag {CacheTag} in HybridCache", tag);
        }

        foreach (var layer in otherLayers)
        {
            try
            {
                await layer.EvictByTagAsync(tag, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(
                    ex,
                    "Failed to invalidate cache tag {CacheTag} in the {CacheLayer}",
                    tag,
                    layer.Layer
                );
            }
        }
    }
}
