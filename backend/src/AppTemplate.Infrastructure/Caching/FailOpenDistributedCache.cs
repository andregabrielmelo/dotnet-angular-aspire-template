using System.Buffers;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace AppTemplate.Infrastructure.Caching;

/// <summary>
/// Redis is optional (see best-practices.md, Health checks). While the client knows the
/// connection is down, these skip Redis entirely instead of letting every cache call wait out
/// StackExchange.Redis's timeout: a read is a miss, a write or eviction does nothing. Requests
/// then run at in-memory/Postgres speed, and Redis is used again as soon as it reconnects.
/// While Redis is down, another instance's L1 (in-memory) entries aren't invalidated, so
/// they can stay stale until <c>LocalCacheExpiration</c>.
/// </summary>
internal sealed class FailOpenDistributedCache(
    IDistributedCache inner,
    IConnectionMultiplexer redis
) : IBufferDistributedCache
{
    private bool Down => !redis.IsConnected;

    public byte[]? Get(string key) => Down ? null : inner.Get(key);

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
        Down ? Task.FromResult<byte[]?>(null) : inner.GetAsync(key, token);

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        if (!Down)
        {
            inner.Set(key, value, options);
        }
    }

    public Task SetAsync(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default
    ) => Down ? Task.CompletedTask : inner.SetAsync(key, value, options, token);

    public void Refresh(string key)
    {
        if (!Down)
        {
            inner.Refresh(key);
        }
    }

    public Task RefreshAsync(string key, CancellationToken token = default) =>
        Down ? Task.CompletedTask : inner.RefreshAsync(key, token);

    public void Remove(string key)
    {
        if (!Down)
        {
            inner.Remove(key);
        }
    }

    public Task RemoveAsync(string key, CancellationToken token = default) =>
        Down ? Task.CompletedTask : inner.RemoveAsync(key, token);

    // HybridCache prefers the buffer API when the store has one (RedisCache does).
    public bool TryGet(string key, IBufferWriter<byte> destination)
    {
        if (Down)
        {
            return false;
        }
        if (inner is IBufferDistributedCache buffered)
        {
            return buffered.TryGet(key, destination);
        }
        var value = inner.Get(key);
        if (value is null)
        {
            return false;
        }
        destination.Write(value);
        return true;
    }

    public async ValueTask<bool> TryGetAsync(
        string key,
        IBufferWriter<byte> destination,
        CancellationToken token = default
    )
    {
        if (Down)
        {
            return false;
        }
        if (inner is IBufferDistributedCache buffered)
        {
            return await buffered.TryGetAsync(key, destination, token);
        }
        var value = await inner.GetAsync(key, token);
        if (value is null)
        {
            return false;
        }
        destination.Write(value);
        return true;
    }

    public void Set(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options)
    {
        if (Down)
        {
            return;
        }
        if (inner is IBufferDistributedCache buffered)
        {
            buffered.Set(key, value, options);
        }
        else
        {
            inner.Set(key, value.ToArray(), options);
        }
    }

    public ValueTask SetAsync(
        string key,
        ReadOnlySequence<byte> value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default
    ) =>
        Down ? ValueTask.CompletedTask
        : inner is IBufferDistributedCache buffered ? buffered.SetAsync(key, value, options, token)
        : new ValueTask(inner.SetAsync(key, value.ToArray(), options, token));
}
