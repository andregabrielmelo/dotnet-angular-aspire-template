using AppTemplate.UseCases.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace AppTemplate.Infrastructure.Caching;

public static class CachingServiceExtensions
{
    public static IServiceCollection AddCaching(
        this IServiceCollection services,
        IConfiguration config,
        ILogger logger
    )
    {
        // Read eagerly: the expirations are needed now to build HybridCache's default entry options
        var options = config.GetSection(CacheOptions.SectionName).Get<CacheOptions>() ?? new();
        if (options.Expiration <= TimeSpan.Zero || options.LocalExpiration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "'Cache:Expiration' and 'Cache:LocalExpiration' must be positive."
            );
        }
        if (options.LocalExpiration > options.Expiration)
        {
            throw new InvalidOperationException(
                "'Cache:LocalExpiration' must not be longer than 'Cache:Expiration'."
            );
        }

        string? connectionString = config.GetConnectionString("cache");
        if (connectionString is not null)
        {
            // HybridCache picks up the registered IDistributedCache as its L2
            services.AddStackExchangeRedisCache(redis =>
            {
                redis.Configuration = connectionString;
                redis.InstanceName = "apptemplate:";
            });

            // No "live" tag: Redis being down makes the app not ready, not dead
            services.AddHealthChecks().AddRedis(connectionString, name: "cache");
        }
        else if (options.AllowLocalOnly)
        {
            logger.LogWarning(
                "No 'cache' connection string found; HybridCache is running L1-only (in-memory, per instance)"
            );
        }
        else
        {
            throw new InvalidOperationException(
                "No valid connection string found. Please ensure that the 'cache' is configured, or set 'Cache:AllowLocalOnly' to run without Redis."
            );
        }

        services.AddHybridCache(hybrid =>
        {
            hybrid.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = options.Expiration,
                LocalCacheExpiration = options.LocalExpiration,
            };
        });

        services.AddSingleton<ICache, HybridCacheService>();

        return services;
    }
}
