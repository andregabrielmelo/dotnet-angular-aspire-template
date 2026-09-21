using AppTemplate.Infrastructure.Data;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.UseCases.Auth;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Identity;

namespace AppTemplate.Web.Configurations;

public static class AuthConfigurations
{
    public static IServiceCollection AddAuthConfigurations(
        this IServiceCollection services,
        IConfiguration configuration,
        Microsoft.Extensions.Logging.ILogger logger
    )
    {
        var jwtSection = configuration.GetSection("Jwt");
        services.Configure<JwtConfiguration>(jwtSection);

        var jwtConfiguration = jwtSection.Get<JwtConfiguration>();
        if (string.IsNullOrWhiteSpace(jwtConfiguration?.SigningKey))
        {
            throw new InvalidOperationException(
                "No Jwt:SigningKey configured. Set one via `dotnet user-secrets set \"Jwt:SigningKey\" \"<a long random string>\"` "
                    + "for local development, or the Jwt__SigningKey environment variable in any shared/production environment."
            );
        }

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                // Loosened to match the Web layer's own FluentValidation rule (min 8 chars) -
                // Identity's stricter defaults (upper+lower+digit+non-alphanumeric) would
                // otherwise reject passwords the request validator already accepted.
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDatabaseContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services
            .AddAuthenticationJwtBearer(
                signingOptions => signingOptions.SigningKey = jwtConfiguration.SigningKey,
                bearerOptions =>
                {
                    bearerOptions.TokenValidationParameters.ValidateIssuer = true;
                    bearerOptions.TokenValidationParameters.ValidIssuer = jwtConfiguration.Issuer;
                    bearerOptions.TokenValidationParameters.ValidateAudience = true;
                    bearerOptions.TokenValidationParameters.ValidAudience =
                        jwtConfiguration.Audience;
                }
            )
            .AddAuthorization();

        services.AddScoped<IIdentityService, IdentityService>();

        logger.LogInformation("{Project} were configured", "Authentication and Authorization");

        return services;
    }
}
