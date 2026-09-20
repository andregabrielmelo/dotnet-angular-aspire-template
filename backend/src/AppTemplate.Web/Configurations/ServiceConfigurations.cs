using System.Text;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Interfaces;
using AppTemplate.Infrastructure;
using AppTemplate.Infrastructure.Auth;
using AppTemplate.Infrastructure.Email;
using AppTemplate.Web.Configurations.Auth;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

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
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        var jwtOptions =
            builder.Configuration.GetSection("Authentication:Jwt").Get<JwtOptions>()
            ?? throw new InvalidOperationException("Missing 'Authentication:Jwt' configuration.");

        var authenticationBuilder = services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.SigningKey)
                    ),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        var google = builder
            .Configuration.GetSection("Authentication:Google")
            .Get<GoogleAuthOptions>();

        // Google's OAuth handler validates ClientId/ClientSecret are non-empty on every request
        // (not just its own callback path - ASP.NET Core's authentication middleware probes
        // every IAuthenticationRequestHandler scheme to see if it owns the current request), so
        // registering it with blank credentials would 500 the entire API. Only wire it up once
        // real credentials are configured; sign-in with Google is an optional feature.
        if (!string.IsNullOrEmpty(google?.ClientId) && !string.IsNullOrEmpty(google.ClientSecret))
        {
            authenticationBuilder.AddGoogle(
                GoogleDefaults.AuthenticationScheme,
                options =>
                {
                    // SignInScheme is irrelevant here: ExternalLoginCallbackHandler calls
                    // context.HandleResponse() and issues our own JWT pair directly, so the
                    // default post-external-login sign-in never runs.
                    options.ClientId = google.ClientId;
                    options.ClientSecret = google.ClientSecret;
                    options.CallbackPath = "/auth/external/google-callback";
                    options.Events.OnTicketReceived = ExternalLoginCallbackHandler.HandleAsync;
                }
            );
        }

        services.AddAuthorization();

        logger.LogInformation(
            "{Project} services registered",
            "Mediator Source Generator, Email Sender, and Authentication"
        );

        return services;
    }
}
