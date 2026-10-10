using AppTemplate.Infrastructure.Jobs.Extensions;
using AppTemplate.Infrastructure.Outbox.Triggers;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AppTemplate.Infrastructure.Outbox;

public static class OutboxServiceExtensions
{
    /// <summary>
    /// The transactional outbox and inbox (ADR 016): integration events raised by entities are
    /// saved with them and delivered at least once by a Hangfire relay. Every event type must
    /// be registered here. To remove the outbox, see docs/content/reliability-semantics.md.
    /// </summary>
    public static IServiceCollection AddOutbox(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<OutboxOptions> registerEvents
    )
    {
        services
            .AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SectionName))
            .Configure(registerEvents)
            .Validate(options =>
                options.BatchSize > 0
                && options.MaxAttempts > 0
                && options.LeaseDuration > TimeSpan.Zero
            )
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IntegrationEventRegistry>();
        services.AddScoped<IOutboxTrigger, HangfireOutboxTrigger>();
        services.AddScoped<ISaveChangesInterceptor, OutboxInterceptor>();
        services.AddSingleton<OutboxProcessor>();
        services.AddTransient<ProcessOutboxJob>();
        services.AddRecurringJob<OutboxSweepJob>();
        services.AddScoped<IOutboxAdministration, OutboxAdministration>();

        return services;
    }
}
