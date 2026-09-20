using AppTemplate.Infrastructure.Auth;
using AppTemplate.Infrastructure.Email;
using AppTemplate.UseCases.Auth;

namespace AppTemplate.Web.Configurations;

public static class OptionConfigurations
{
    public static IServiceCollection AddOptionConfigurations(
        this IServiceCollection services,
        IConfiguration configuration,
        Microsoft.Extensions.Logging.ILogger logger,
        WebApplicationBuilder builder
    )
    {
        services
            .Configure<MailserverConfiguration>(configuration.GetSection("Mailserver"))
            .Configure<JwtOptions>(configuration.GetSection("Authentication:Jwt"))
            .Configure<FrontendOptions>(options =>
            {
                configuration.GetSection("Frontend").Bind(options);

                // Aspire assigns the frontend a dynamic port; when running under the AppHost this
                // env var (injected via api.WithReference(frontend) in AppHost.cs) reflects the
                // real address and overrides the static "Frontend:BaseUrl" appsetting above.
                var aspireFrontendUrl = configuration["services:angular:http:0"];
                if (!string.IsNullOrEmpty(aspireFrontendUrl))
                {
                    options.BaseUrl = aspireFrontendUrl;
                }
            })
            .Configure<EmailOptions>(configuration.GetSection("Authentication:Email"));

        logger.LogInformation("{Project} were configured", "Options");

        return services;
    }
}
