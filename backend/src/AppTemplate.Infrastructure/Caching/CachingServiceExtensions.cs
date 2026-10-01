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
        string? connectionString = config.GetConnectionString("cache");
        if (connectionString is not null)
        {
            // HybridCache picks up the registered IDistributedCache as its L2
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = connectionString;
                options.InstanceName = "apptemplate:";
            });
        }
        else if (config.GetValue<bool>("Cache:AllowLocalOnly"))
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

        services.AddHybridCache(options =>
        {
            // Local (L1) entries are not invalidated across instances, so keep them short
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(30),
                LocalCacheExpiration = TimeSpan.FromMinutes(1),
            };
        });

        services.AddSingleton<ICache, HybridCacheService>();

        return services;
    }
}
