using AppTemplate.UseCases.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace AppTemplate.Infrastructure.Caching;

internal sealed class HybridCacheService(HybridCache cache) : ICache
{
    public ValueTask<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        CancellationToken cancellationToken = default
    ) => cache.GetOrCreateAsync(key, factory, cancellationToken: cancellationToken);

    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        cache.RemoveAsync(key, cancellationToken);
}
