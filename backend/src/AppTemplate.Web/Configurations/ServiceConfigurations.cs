using AppTemplate.Core.Interfaces;
using AppTemplate.Infrastructure;
using AppTemplate.Infrastructure.Email;
using AppTemplate.Infrastructure.Identity;

namespace AppTemplate.Web.Configurations;

public static class ServiceConfigurations
{
    public static IServiceCollection AddServiceConfigurations(
        this IServiceCollection services,
        Microsoft.Extensions.Logging.ILogger logger,
        WebApplicationBuilder builder
    )
    {
        services
            .AddInfrastructureServices(builder.Configuration, logger)
            .AddMediatorSourceGenerator(logger);

        services.AddScoped<IEmailSender, MimeKitEmailSender>();

        services.AddKeycloakAdministration();

        logger.LogInformation(
            "{Project} services registered",
            "Mediator Source Generator and Email Sender"
        );

        return services;
    }
}
