using AppTemplate.Infrastructure.Jobs.Options;
using AppTemplate.Infrastructure.Jobs.Services;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Jobs.Extensions;

public static class ApplicationBuilderExtensions
{
    public const string DashboardPath = "/hangfire";

    /// <summary>
    /// Maps the Hangfire dashboard (Development only) and schedules every recurring job
    /// definition. Call after authentication/authorization middleware.
    /// <para>
    /// The dashboard can delete and re-run jobs, so it's kept to Development and local requests
    /// (Hangfire's own default). Other environments use the permission-gated admin API.
    /// </para>
    /// </summary>
    public static async Task<WebApplication> UseJobSchedulingAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default
    )
    {
        var options = app.Services.GetRequiredService<IOptions<JobSchedulingOptions>>().Value;
        if (!options.Enabled)
        {
            app.Logger.LogInformation("Job scheduling is disabled by configuration");
            return app;
        }

        if (app.Environment.IsDevelopment())
        {
            app.MapHangfireDashboard(
                DashboardPath,
                new DashboardOptions
                {
                    DashboardTitle = "AppTemplate jobs",
                    Authorization = [new LocalRequestsOnlyAuthorizationFilter()],
                }
            );
        }

        await using var scope = app.Services.CreateAsyncScope();
        await scope
            .ServiceProvider.GetRequiredService<RecurringJobRegistrar>()
            .RegisterAllAsync(cancellationToken);

        return app;
    }
}
