namespace AppTemplate.Web.Configurations;

public static class AuthenticationConfigurations
{
    /// <summary>
    /// The API only accepts OAuth 2.0 access tokens (JWTs) issued by Keycloak for this API's
    /// audience. Browsers never call it directly - AppTemplate.BackendForFrontend holds the
    /// user's session cookie and attaches the access token as a Bearer header when proxying.
    /// See ADR 007.
    /// </summary>
    public static IServiceCollection AddAuthenticationConfigurations(
        this IServiceCollection services,
        Microsoft.Extensions.Logging.ILogger logger,
        WebApplicationBuilder builder
    )
    {
        var keycloak = builder.Configuration.GetSection("Keycloak");

        services
            .AddAuthentication()
            .AddKeycloakJwtBearer(
                serviceName: "keycloak",
                realm: keycloak["Realm"] ?? "apptemplate",
                options =>
                {
                    options.Audience = keycloak["Audience"] ?? "apptemplate-api";
                    // Keycloak runs over plain HTTP inside Aspire locally.
                    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                    // The Aspire service-discovery address is for local development. Elsewhere,
                    // set Keycloak:Authority to the realm's public HTTPS URL (the tokens' issuer).
                    if (keycloak["Authority"] is { Length: > 0 } authority)
                    {
                        options.Authority = authority;
                    }
                    // Keep JWT claim names ("sub", "email", ...) instead of the legacy
                    // WS-Federation URIs ASP.NET Core maps them to by default.
                    options.MapInboundClaims = false;
                }
            );

        services.AddAuthorization();

        logger.LogInformation("{Project} were configured", "Authentication and Authorization");

        return services;
    }
}
