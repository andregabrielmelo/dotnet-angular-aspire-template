using AppTemplate.Infrastructure.Caching;
using Microsoft.AspNetCore.OutputCaching;

namespace AppTemplate.Web.Caching;

/// <summary>
/// Adds the HTTP output cache, which only this host has, to Infrastructure's cache
/// invalidation: whenever use-case data drops a tag, whole responses carrying it go too.
/// </summary>
internal sealed class OutputCacheTagEvictor(IOutputCacheStore outputCacheStore) : ICacheTagEvictor
{
    public string Layer => "output cache";

    public ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken) =>
        outputCacheStore.EvictByTagAsync(tag, cancellationToken);
}
