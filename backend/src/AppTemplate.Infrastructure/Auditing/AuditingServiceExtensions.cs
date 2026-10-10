using AppTemplate.Infrastructure.Data.Queries;
using AppTemplate.Infrastructure.Jobs.Extensions;
using AppTemplate.Infrastructure.Jobs.RecurringJobs;
using AppTemplate.UseCases.Auditing;
using AppTemplate.UseCases.Auditing.List;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AppTemplate.Infrastructure.Auditing;

public static class AuditingServiceExtensions
{
    /// <summary>
    /// The audit log (ADR 017): entity changes of allowlisted properties, explicit events
    /// (<see cref="IAuditLog"/>), the query service and the retention job. With
    /// <c>Audit:Enabled=false</c> only a no-op <see cref="IAuditLog"/> is registered, so the
    /// rest of the app runs unchanged.
    /// </summary>
    public static IServiceCollection AddAuditing(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AuditOptions> configure
    )
    {
        services
            .AddOptions<AuditOptions>()
            .Bind(configuration.GetSection(AuditOptions.SectionName))
            .Configure(configure)
            .Validate(options => options.RetentionDays > 0, "Audit:RetentionDays must be positive.")
            .ValidateOnStart();
        services.TryAddScoped<AuditActorContext>();
        // Reading stays available when recording is off: the log just stops growing.
        services.AddScoped<IListAuditEntriesQueryService, ListAuditEntriesQueryService>();

        if (!configuration.GetValue(AuditOptions.SectionName + ":Enabled", true))
        {
            services.AddScoped<IAuditLog, NullAuditLog>();
            return services;
        }

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ISaveChangesInterceptor, AuditInterceptor>();
        services.AddScoped<IAuditLog, AuditLog>();
        services.AddRecurringJob<AuditRetentionJob>();

        return services;
    }
}
