using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AppTemplate.UnitTests;

/// <summary>Real HybridCache instances for handler tests - caching behavior is what's under test.</summary>
public static class TestCaches
{
    /// <summary>An in-memory (L1 only) HybridCache.</summary>
    public static HybridCache Create() => Create(distributedCache: null);

    /// <summary>
    /// A HybridCache whose L2 is <paramref name="distributedCache"/>. Two instances sharing one
    /// distributed cache behave like two API instances sharing Redis: values really go through
    /// serialization.
    /// </summary>
    public static HybridCache Create(IDistributedCache? distributedCache)
    {
        var services = new ServiceCollection();
        if (distributedCache is not null)
        {
            services.AddSingleton(distributedCache);
        }
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    /// <summary>
    /// Stands in for Redis. (HybridCache deliberately ignores a plain MemoryDistributedCache as
    /// its L2, since that would just duplicate L1 - so it is wrapped in a different type.)
    /// </summary>
    public static IDistributedCache CreateSharedDistributedCache() =>
        new SharedDistributedCache(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()))
        );

    private sealed class SharedDistributedCache(IDistributedCache inner) : IDistributedCache
    {
        public byte[]? Get(string key) => inner.Get(key);

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            inner.GetAsync(key, token);

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            inner.Set(key, value, options);

        public Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
            CancellationToken token = default
        ) => inner.SetAsync(key, value, options, token);

        public void Refresh(string key) => inner.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default) =>
            inner.RefreshAsync(key, token);

        public void Remove(string key) => inner.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default) =>
            inner.RemoveAsync(key, token);
    }
}
