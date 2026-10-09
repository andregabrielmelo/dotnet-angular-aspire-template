using System.Buffers;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace AppTemplate.Web.Caching;

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

/// <summary>The output cache's equivalent of <see cref="FailOpenDistributedCache"/>.</summary>
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

/// <summary>
/// Degraded, never Unhealthy, when Redis is unreachable: it's an optional dependency. Reads
/// the client's connection state and pings only when connected, so the probe itself never
/// waits out a timeout.
/// </summary>
internal sealed class RedisConnectionHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        if (!redis.IsConnected)
        {
            return new HealthCheckResult(context.Registration.FailureStatus);
        }

        try
        {
            await redis.GetDatabase().PingAsync().WaitAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: exception);
        }
    }
}

internal static class ServiceCollectionDecoratorExtensions
{
    /// <summary>Wraps the last registration of <typeparamref name="TService"/>, keeping its lifetime.</summary>
    public static IServiceCollection Decorate<TService>(
        this IServiceCollection services,
        Func<IServiceProvider, TService, TService> decorate
    )
        where TService : class
    {
        var descriptor = services.Last(d => d.ServiceType == typeof(TService) && !d.IsKeyedService);
        services.Remove(descriptor);
        services.Add(
            ServiceDescriptor.Describe(
                typeof(TService),
                provider => decorate(provider, (TService)Create(provider, descriptor)),
                descriptor.Lifetime
            )
        );
        return services;
    }

    private static object Create(IServiceProvider provider, ServiceDescriptor descriptor) =>
        descriptor.ImplementationInstance
        ?? descriptor.ImplementationFactory?.Invoke(provider)
        ?? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
}
