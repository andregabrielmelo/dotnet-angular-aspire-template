using AppTemplate.UseCases.Caching;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Hybrid;

namespace AppTemplate.Web.Caching;

/// <summary>
/// Invalidates a tag in both caching layers: HybridCache (use-case data) and the HTTP output
/// cache (whole responses). With Redis configured, both reach every API instance: HybridCache
/// records the tag invalidation in L2, and the output cache store is Redis itself.
/// </summary>
public sealed class CacheInvalidator(HybridCache hybridCache, IOutputCacheStore outputCacheStore)
    : ICacheInvalidator
{
    public async ValueTask InvalidateAsync(string tag, CancellationToken cancellationToken)
    {
        await hybridCache.RemoveByTagAsync(tag, cancellationToken);
        await outputCacheStore.EvictByTagAsync(tag, cancellationToken);
    }
}
