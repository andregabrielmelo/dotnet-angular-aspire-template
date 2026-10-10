using AppTemplate.Infrastructure.Jobs.Options;
using AppTemplate.Infrastructure.Jobs.RecurringJobs;
using AppTemplate.Infrastructure.Jobs.Services;
using Hangfire;
using Hangfire.Logging;
using Hangfire.PostgreSql;
using Hangfire.PostgreSql.Factories;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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

        // Registered directly rather than with UsePostgreSqlStorage(), which also sets the static
        // JobStorage.Current - process-wide state this project avoids.
        services.AddSingleton<JobStorage>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<JobSchedulingOptions>>().Value;
            var storageOptions = new PostgreSqlStorageOptions
            {
                SchemaName = "hangfire",
                PrepareSchemaIfNecessary = options.PrepareSchema,
                // Postgres can still be starting when the API starts: retry the first connection
                // with back-off, then fail rather than run without storage.
                StartupConnectionMaxRetries = 5,
                AllowDegradedModeWithoutStorage = false,
            };
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

        // Whether this process runs the server is read from the registered options when the
        // host starts, never at registration: Hangfire's hosted service is wrapped so it's
        // built only when RunServer is on.
        var firstAdded = services.Count;
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
        RunOnlyWhenServerEnabled(services, firstAdded);

        // Recurring jobs - add new ones here.
        services.AddRecurringJob<SyncUserProfilesJob>();

        services.AddScoped<RecurringJobRunner>();
        services.AddScoped<RecurringJobRegistrar>();
        services.AddScoped<IJobManagementService, JobManagementService>();

        return services;
    }

    /// <summary>
    /// Replaces the hosted services registered from <paramref name="firstAdded"/> on (Hangfire's
    /// server) with factories that build them only when <see cref="JobSchedulingOptions.RunServer"/>
    /// is on, and otherwise a hosted service that does nothing.
    /// </summary>
    private static void RunOnlyWhenServerEnabled(IServiceCollection services, int firstAdded)
    {
        for (var index = firstAdded; index < services.Count; index++)
        {
            var descriptor = services[index];
            if (descriptor.ServiceType != typeof(IHostedService) || descriptor.IsKeyedService)
            {
                continue;
            }

            services[index] = ServiceDescriptor.Describe(
                typeof(IHostedService),
                provider =>
                    provider.GetRequiredService<IOptions<JobSchedulingOptions>>().Value.RunServer
                        ? descriptor.ImplementationInstance
                            ?? descriptor.ImplementationFactory?.Invoke(provider)
                            ?? ActivatorUtilities.CreateInstance(
                                provider,
                                descriptor.ImplementationType!
                            )
                        : new DisabledJobServer(),
                descriptor.Lifetime
            );
        }
    }

    /// <summary>Stands in for Hangfire's server when <c>JobScheduling:RunServer</c> is off.</summary>
    private sealed class DisabledJobServer : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
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
