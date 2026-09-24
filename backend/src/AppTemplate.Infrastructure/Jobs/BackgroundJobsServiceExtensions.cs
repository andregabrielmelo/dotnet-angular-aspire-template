using AppTemplate.UseCases.Jobs;
using Hangfire;
using Hangfire.Logging;
using Hangfire.PostgreSql;
using Hangfire.PostgreSql.Factories;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Jobs;

public static class BackgroundJobsServiceExtensions
{
    /// <summary>
    /// Hangfire with Postgres storage (its own schema in the application database), a server
    /// processing <see cref="JobQueues.All"/> in priority order, and DI-activated job classes.
    /// The storage is registered explicitly in DI (not via Hangfire's global JobStorage.Current),
    /// so tests can swap in in-memory storage.
    /// </summary>
    public static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString
    )
    {
        services
            .AddOptions<BackgroundJobsOptions>()
            .Bind(configuration.GetSection(BackgroundJobsOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<BackgroundJobsOptions>,
            BackgroundJobsOptionsValidator
        >();

        services.AddSingleton<JobStorage>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<BackgroundJobsOptions>>().Value;
            var storageOptions = new PostgreSqlStorageOptions
            {
                SchemaName = options.Schema,
                PrepareSchemaIfNecessary = true,
            };
            return new PostgreSqlStorage(
                new NpgsqlConnectionFactory(connectionString, storageOptions),
                storageOptions
            );
        });

        services.AddHangfire(
            (provider, configuration) =>
            {
                configuration
                    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                    .UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings();

                // Hangfire's log provider is process-wide static state. By default it logs
                // through this host's ILoggerFactory, which is right for a normal app (one host
                // per process). A registered ILogProvider takes precedence - tests use that,
                // since they run several hosts in one process and dispose them independently.
                if (provider.GetService<ILogProvider>() is { } logProvider)
                {
                    configuration.UseLogProvider(logProvider);
                }
            }
        );

        if (
            configuration.GetValue(
                $"{BackgroundJobsOptions.SectionName}:{nameof(BackgroundJobsOptions.RunServer)}",
                defaultValue: true
            )
        )
        {
            services.AddHangfireServer(
                (provider, server) =>
                {
                    var options = provider
                        .GetRequiredService<IOptions<BackgroundJobsOptions>>()
                        .Value;
                    server.Queues = JobQueues.All;
                    server.WorkerCount = options.WorkerCount;
                }
            );
        }

        services.AddScoped<WelcomeEmailJob>();
        services.AddScoped<SyncUserProfilesJob>();
        services.AddScoped<IBackgroundJobScheduler, HangfireBackgroundJobScheduler>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }

    /// <summary>
    /// Creates or updates the recurring job definitions. Idempotent (stable ids), so it is
    /// safe on every startup of every instance.
    /// </summary>
    public static IServiceProvider RegisterRecurringJobs(this IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<BackgroundJobsOptions>>().Value;
        var recurringJobs = services.GetRequiredService<IRecurringJobManager>();

        recurringJobs.AddOrUpdate<SyncUserProfilesJob>(
            SyncUserProfilesJob.RecurringJobId,
            JobQueues.Default,
            job => job.RunAsync(CancellationToken.None),
            options.SyncUserProfilesCron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc }
        );

        return services;
    }
}
