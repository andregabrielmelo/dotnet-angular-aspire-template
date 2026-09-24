using Hangfire;
using Hangfire.Dashboard;

namespace AppTemplate.Web.Configurations;

public static class BackgroundJobsConfigurations
{
    public const string DashboardPath = "/jobs";

    /// <summary>
    /// The Hangfire dashboard can retry, delete and trigger jobs, so it's only exposed in
    /// Development, and only to requests from the local machine. For other environments, host
    /// it behind the backend for frontend with a permission check (see ADR 012).
    /// </summary>
    public static WebApplication MapBackgroundJobsDashboard(this WebApplication app)
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
            );
        }

        return app;
    }
}
