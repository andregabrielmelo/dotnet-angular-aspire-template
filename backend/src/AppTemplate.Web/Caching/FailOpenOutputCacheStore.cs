using Microsoft.AspNetCore.OutputCaching;
using StackExchange.Redis;

namespace AppTemplate.Web.Caching;

/// <summary>
/// The output cache's equivalent of Infrastructure's <c>FailOpenDistributedCache</c>: while
/// Redis is disconnected, reads miss and writes and evictions do nothing.
/// </summary>
internal sealed class FailOpenOutputCacheStore(
    IOutputCacheStore inner,
    IConnectionMultiplexer redis
) : IOutputCacheStore
{
    private bool Down => !redis.IsConnected;

    public ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken) =>
        Down ? ValueTask.FromResult<byte[]?>(null) : inner.GetAsync(key, cancellationToken);

    public ValueTask SetAsync(
        string key,
        byte[] value,
        string[]? tags,
        TimeSpan validFor,
        CancellationToken cancellationToken
    ) =>
        Down
            ? ValueTask.CompletedTask
            : inner.SetAsync(key, value, tags, validFor, cancellationToken);

    public ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken) =>
        Down ? ValueTask.CompletedTask : inner.EvictByTagAsync(tag, cancellationToken);
}
