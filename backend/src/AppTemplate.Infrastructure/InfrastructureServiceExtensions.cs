using AppTemplate.Infrastructure.Data;
using AppTemplate.Infrastructure.Data.Queries;
using AppTemplate.UseCases.Users.List;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AppTemplate.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfigurationManager config,
        ILogger logger
    )
    {
        string? connectionString = config.GetConnectionString("apptemplate");
        if (connectionString is null)
        {
            throw new InvalidOperationException(
                "No valid connection string found. Please ensure that the 'apptemplate' is configured."
            );
        }

        services.AddScoped<EventDispatchInterceptor>();
        services.AddScoped<IDomainEventDispatcher, MediatorDomainEventDispatcher>();

        services.AddDbContext<ApplicationDatabaseContext>(
            (provider, options) =>
            {
                var eventDispatchInterceptor =
                    provider.GetRequiredService<EventDispatchInterceptor>();

                // Use PostgreSQL as the database provider
                options
                    .UseNpgsql(
                        connectionString,
                        options => options.MigrationsHistoryTable("__EFMigrationsHistory", "wfc")
                    )
                    .UseSnakeCaseNamingConvention();

                options.AddInterceptors(eventDispatchInterceptor);

                // TODO: Reavaluate the need for this
                // Change PendingModelChangesWarning to Log to avoid exceptions when the model changes without a new migration
                // workround to bug when starting a new database with initial migration
                options.ConfigureWarnings(warnings =>
                    warnings.Log(RelationalEventId.PendingModelChangesWarning)
                );
            }
        );

        services
            .AddScoped(typeof(IRepository<>), typeof(EntityFrameworkRepository<>))
            .AddScoped<IListUsersQueryService, ListUsersQueryService>();

        logger.LogInformation("{Project} services registered", "Infrastructure");

        return services;
    }
}
