using AppTemplate.Infrastructure.Jobs.Extensions;
using AppTemplate.UseCases.Idempotency;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AppTemplate.Infrastructure.Idempotency;

public static class IdempotencyServiceExtensions
{
    /// <summary>
    /// Durable idempotency keys (ADR 019). Without this registration the pipeline behavior finds
    /// no store and every command simply runs; see "Removing it" in ADR 019.
    /// </summary>
    public static IServiceCollection AddIdempotency(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<IdempotencyOptions>()
            .Bind(configuration.GetSection(IdempotencyOptions.SectionName))
            .Validate(options => options.RecordLifetime > TimeSpan.Zero)
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();
        services.AddRecurringJob<IdempotencyCleanupJob>();
        return services;
    }
}
