using AppTemplate.UseCases.Caching;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Hybrid;

namespace AppTemplate.Web.Caching;

/// <summary>
/// Invalidates a tag in both caching layers: HybridCache (use-case data) and the HTTP output
/// cache (whole responses). With Redis configured, both reach every API instance: HybridCache
/// records the tag invalidation in L2, and the output cache store is Redis itself.
/// Best-effort: it runs after the write has been saved, so a cache failure (Redis down) is
/// logged instead of thrown. Stale entries then live until they expire.
/// </summary>
public sealed class CacheInvalidator(
    HybridCache hybridCache,
    IOutputCacheStore outputCacheStore,
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

        try
        {
            await outputCacheStore.EvictByTagAsync(tag, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Failed to invalidate cache tag {CacheTag} in the output cache",
                tag
            );
        }
    }
}
