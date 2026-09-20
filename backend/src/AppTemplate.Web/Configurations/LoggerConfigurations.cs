using Serilog;

namespace AppTemplate.Web.Configurations;

public static class LoggerConfigurations
{
    public static WebApplicationBuilder AddLoggerConfigurations(this WebApplicationBuilder builder)
    {
        // Add Serilog as an additional logging provider alongside OpenTelemetry
        // This allows both Serilog (for console/file) and OpenTelemetry (for Aspire) to work together
        builder.Logging.AddSerilog(
            new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
                .WriteTo.Console()
                .CreateLogger()
        );

        return builder;
    }
}
