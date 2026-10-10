using AppTemplate.UseCases.Caching;
using AppTemplate.UseCases.Idempotency;
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
    IUnitOfWork unitOfWork,
    ILogger<CacheInvalidator> logger
) : ICacheInvalidator
{
    public ValueTask InvalidateAsync(string tag, CancellationToken cancellationToken)
    {
        // Inside a transaction the write isn't visible yet: invalidating now would let a
        // concurrent read re-cache the old data, so wait for the commit.
        if (unitOfWork.InTransaction)
        {
            unitOfWork.AfterCommit(token => InvalidateNowAsync(tag, token));
            return ValueTask.CompletedTask;
        }
        return InvalidateNowAsync(tag, cancellationToken);
    }

    private async ValueTask InvalidateNowAsync(string tag, CancellationToken cancellationToken)
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
