using AppTemplate.UseCases.Authorization;
using AppTemplate.Web.Authorization;
using Microsoft.AspNetCore.Authentication;

namespace AppTemplate.Web.Configurations;

public static class AuthorizationConfigurations
{
    /// <summary>
    /// One policy per permission (policy name = permission name), satisfied by the
    /// matching permission claim that <see cref="KeycloakPermissionsClaimsTransformation"/>
    /// derives from the access token. Endpoints opt in with FastEndpoints' <c>Policies(...)</c>;
    /// everything else only requires an authenticated user (FastEndpoints' default).
    /// </summary>
    public static IServiceCollection AddAuthorizationConfigurations(
        this IServiceCollection services,
        Microsoft.Extensions.Logging.ILogger logger,
        WebApplicationBuilder builder
    )
    {
        services
            .AddOptions<KeycloakAuthorizationOptions>()
            .Configure(options =>
                options.ApiClientId =
                    builder.Configuration["Keycloak:Audience"] ?? options.ApiClientId
            );
        services.AddTransient<IClaimsTransformation, KeycloakPermissionsClaimsTransformation>();

        var authorization = services.AddAuthorizationBuilder();
        foreach (var permission in Permission.All)
        {
            authorization.AddPolicy(
                permission,
                policy =>
                    policy
                        .RequireAuthenticatedUser()
                        .RequireClaim(
                            KeycloakPermissionsClaimsTransformation.PermissionClaimType,
                            permission
                        )
            );
        }

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        logger.LogInformation("{Project} were configured", "Permission policies");

        return services;
    }
}
