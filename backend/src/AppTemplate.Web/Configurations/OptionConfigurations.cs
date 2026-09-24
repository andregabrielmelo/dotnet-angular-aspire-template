using AppTemplate.Infrastructure.Email;
using AppTemplate.UseCases.Users.SendWelcomeEmail;

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
            .Configure<WelcomeEmailOptions>(
                configuration.GetSection(WelcomeEmailOptions.SectionName)
            )
            // Configure Web Behavior
            .Configure<CookiePolicyOptions>(options =>
            {
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

        logger.LogInformation("{Project} were configured", "Options");

        return services;
    }
}
