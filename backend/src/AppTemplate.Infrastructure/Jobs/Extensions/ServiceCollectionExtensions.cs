using AppTemplate.Infrastructure.Jobs.FireAndForget;
using AppTemplate.Infrastructure.Jobs.Options;
using AppTemplate.Infrastructure.Jobs.RecurringJobs;
using AppTemplate.Infrastructure.Jobs.Services;
using AppTemplate.UseCases.Jobs;
using Hangfire;
using Hangfire.Logging;
using Hangfire.PostgreSql;
using Hangfire.PostgreSql.Factories;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Jobs.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Hangfire (Postgres storage in the application database's <c>hangfire</c>
    /// schema), its server, and every job. Pair with <c>UseJobSchedulingAsync</c> at startup.
    /// Storage and managers come from DI - never Hangfire's static <c>JobStorage.Current</c> /
    /// <c>RecurringJob</c> APIs - so tests can swap in in-memory storage per host.
    /// </summary>
    public static IServiceCollection AddJobScheduling(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString
    )
    {
        services
            .AddOptions<JobSchedulingOptions>()
            .Bind(configuration.GetSection(JobSchedulingOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<JobSchedulingOptions>,
            JobSchedulingOptionsValidator
        >();
        services.TryAddSingleton(TimeProvider.System);

        var options =
            configuration.GetSection(JobSchedulingOptions.SectionName).Get<JobSchedulingOptions>()
            ?? new JobSchedulingOptions();

        if (!options.Enabled)
        {
            services.AddScoped<IBackgroundJobScheduler, DisabledBackgroundJobScheduler>();
            services.AddScoped<IJobManagementService, DisabledJobManagementService>();
            return services;
        }

        services.AddSingleton<JobStorage>(_ =>
        {
            var storageOptions = new PostgreSqlStorageOptions { PrepareSchemaIfNecessary = true };
            return new PostgreSqlStorage(
                new NpgsqlConnectionFactory(connectionString, storageOptions),
                storageOptions
            );
        });

        services.AddHangfire(
            (provider, hangfire) =>
            {
                hangfire
                    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                    .UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings();

                // Hangfire's log provider is process-wide static state. By default it logs
                // through this host's ILoggerFactory, which is right for a normal app (one host
                // per process). A registered ILogProvider takes precedence - tests use that,
                // since they run several hosts in one process and dispose them independently.
                if (provider.GetService<ILogProvider>() is { } logProvider)
                {
                    hangfire.UseLogProvider(logProvider);
                }
            }
        );

        if (options.RunServer)
        {
            services.AddHangfireServer(
                (provider, server) =>
                {
                    server.Queues = JobQueues.All;
                    server.WorkerCount = provider
                        .GetRequiredService<IOptions<JobSchedulingOptions>>()
                        .Value.WorkerCount;
                    server.ServerTimeout = TimeSpan.FromMinutes(5);
                    server.ShutdownTimeout = TimeSpan.FromSeconds(30);
                }
            );
        }

        // Recurring jobs - add new ones here.
        services.AddRecurringJob<SyncUserProfilesJob>();

        // Fire-and-forget jobs - Hangfire resolves them from DI when they run.
        services.AddScoped<WelcomeEmailJob>();

        services.AddScoped<RecurringJobRunner>();
        services.AddScoped<RecurringJobRegistrar>();
        services.AddScoped<IBackgroundJobScheduler, HangfireBackgroundJobScheduler>();
        services.AddScoped<IJobManagementService, JobManagementService>();

        return services;
    }

    /// <summary>
    /// Registers a recurring job definition, both as itself and as
    /// <see cref="IRecurringJobDefinition"/> (so the registrar and runner discover it).
    /// </summary>
    public static IServiceCollection AddRecurringJob<TJob>(this IServiceCollection services)
        where TJob : class, IRecurringJobDefinition
    {
        services.AddScoped<TJob>();
        services.AddScoped<IRecurringJobDefinition>(provider =>
            provider.GetRequiredService<TJob>()
        );
        return services;
    }
}
