using AppTemplate.UseCases.Caching;
using AppTemplate.Web.Caching;
using Microsoft.Extensions.Caching.Hybrid;

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
            builder.AddRedisDistributedCache(RedisConnectionName);
            builder.AddRedisOutputCache(RedisConnectionName);
        }

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

        services.AddScoped<ICacheInvalidator, CacheInvalidator>();

        logger.LogInformation(
            "{Project} were configured ({Store})",
            "HybridCache and output caching",
            useRedis ? "Redis" : "in-memory"
        );

        return services;
    }
}
