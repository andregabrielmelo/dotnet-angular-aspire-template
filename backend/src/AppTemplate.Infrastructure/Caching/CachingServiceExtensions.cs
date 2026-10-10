using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace AppTemplate.Infrastructure.Caching;

public static class CachingServiceExtensions
{
    /// <summary>The health check's tag in ServiceDefaults: an optional dependency, never readiness.</summary>
    private const string DependencyTag = "dependency";

    /// <summary>
    /// HybridCache for use-case data (ADR 011) and its invalidation. L1 is in-memory; L2 is
    /// whatever <see cref="IDistributedCache"/> the host registers (Redis under Aspire), or
    /// nothing, in which case both layers are in-memory.
    /// </summary>
    public static IServiceCollection AddApplicationCaching(this IServiceCollection services)
    {
        services.AddHybridCache(options =>
        {
            options.MaximumPayloadBytes = 1024 * 1024;
            options.MaximumKeyLength = 512;
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromMinutes(1),
            };
        });
        services.AddScoped<ICacheInvalidator, CacheInvalidator>();
        return services;
    }

    /// <summary>
    /// Makes the Redis <see cref="IDistributedCache"/> the host registered fail open (see
    /// <see cref="FailOpenDistributedCache"/>) and adds a dependency-only health check for it.
    /// Call after the host's Redis registration; Redis stays optional at runtime.
    /// </summary>
    public static IServiceCollection AddFailOpenRedisCache(
        this IServiceCollection services,
        string healthCheckName
    )
    {
        services.Decorate<IDistributedCache>(
            (provider, redis) =>
                new FailOpenDistributedCache(
                    redis,
                    provider.GetRequiredService<IConnectionMultiplexer>()
                )
        );
        services
            .AddHealthChecks()
            .AddCheck<RedisConnectionHealthCheck>(
                healthCheckName,
                failureStatus: HealthStatus.Degraded,
                tags: [DependencyTag],
                timeout: TimeSpan.FromSeconds(3)
            );
        return services;
    }
}
