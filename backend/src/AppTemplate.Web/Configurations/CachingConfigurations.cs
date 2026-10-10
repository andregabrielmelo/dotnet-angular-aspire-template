using AppTemplate.Infrastructure.Caching;
using AppTemplate.UseCases.Caching;
using AppTemplate.Web.Caching;
using Microsoft.AspNetCore.OutputCaching;
using StackExchange.Redis;

namespace AppTemplate.Web.Configurations;

public static class CachingConfigurations
{
    public const string RedisConnectionName = "cache";
    public const string UsersListPolicy = "users-list";

    /// <summary>
    /// Two layers:
    /// - HybridCache: application data inside use cases. L1 is in-memory; L2 is Redis when
    ///   Aspire provides the "cache" connection.
    /// - Output caching: whole HTTP responses for shared, read-mostly endpoints.
    /// Without Redis (tests, or running the API alone) both fall back to in-memory, which is
    /// correct for a single instance.
    /// </summary>
    public static IServiceCollection AddCachingConfigurations(
        this IServiceCollection services,
        Microsoft.Extensions.Logging.ILogger logger,
        WebApplicationBuilder builder
    )
    {
        var useRedis = !string.IsNullOrEmpty(
            builder.Configuration.GetConnectionString(RedisConnectionName)
        );
        if (useRedis)
        {
            // IDistributedCache (HybridCache's L2) and the output cache store, from Aspire.
            // Aspire's own Redis health checks are untagged, so they would gate readiness;
            // Redis is optional, so it gets a dependency-only check instead (below).
            builder.AddRedisDistributedCache(
                RedisConnectionName,
                settings => settings.DisableHealthChecks = true
            );
            builder.AddRedisOutputCache(
                RedisConnectionName,
                settings => settings.DisableHealthChecks = true
            );
            // Infrastructure makes the distributed cache fail open and adds its dependency check;
            // the output cache is this host's own.
            services.AddFailOpenRedisCache(RedisConnectionName);
            services.Decorate<IOutputCacheStore>(
                (provider, redis) =>
                    new FailOpenOutputCacheStore(
                        redis,
                        provider.GetRequiredService<IConnectionMultiplexer>()
                    )
            );
        }

        services.AddApplicationCaching();

        services.AddOutputCache(options =>
        {
            options.AddPolicy(
                UsersListPolicy,
                policy =>
                    policy
                        .AddPolicy<AuthorizedSharedResponsePolicy>()
                        .Expire(TimeSpan.FromSeconds(60))
                        .Tag(CacheTags.Users),
                excludeDefaultPolicy: true
            );
        });

        // Infrastructure's invalidator evicts HybridCache, then every ICacheTagEvictor.
        services.AddSingleton<ICacheTagEvictor, OutputCacheTagEvictor>();

        logger.LogInformation(
            "{Project} were configured ({Store})",
            "HybridCache and output caching",
            useRedis ? "Redis" : "in-memory"
        );

        return services;
    }
}
