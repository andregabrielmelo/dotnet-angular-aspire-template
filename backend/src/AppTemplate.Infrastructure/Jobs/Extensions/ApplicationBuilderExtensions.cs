using AppTemplate.Infrastructure.Jobs.Options;
using AppTemplate.Infrastructure.Jobs.Services;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

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
        if (app.Environment.IsDevelopment())
        {
            app.MapHangfireDashboard(
                    DashboardPath,
                    new DashboardOptions
                    {
                        DashboardTitle = "AppTemplate jobs",
                        Authorization = [new LocalRequestsOnlyAuthorizationFilter()],
                    }
                )
                // Guarded by its own local-requests-only filter; a browser has no bearer token.
                .AllowAnonymous();
        }

        await using var scope = app.Services.CreateAsyncScope();
        await scope
            .ServiceProvider.GetRequiredService<RecurringJobRegistrar>()
            .RegisterAllAsync(cancellationToken);

        return app;
    }
}
