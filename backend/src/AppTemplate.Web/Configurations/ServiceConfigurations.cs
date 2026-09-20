using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Interfaces;
using AppTemplate.Infrastructure;
using AppTemplate.Infrastructure.Email;
using Microsoft.AspNetCore.Identity;

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

        // Stateless/thread-safe, so a singleton is fine (and avoids per-request allocation).
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        logger.LogInformation(
            "{Project} services registered",
            "Mediator Source Generator and Email Sender"
        );

        return services;
    }
}
