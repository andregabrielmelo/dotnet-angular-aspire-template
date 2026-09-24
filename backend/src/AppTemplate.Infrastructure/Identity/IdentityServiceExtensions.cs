using AppTemplate.UseCases.Users.ForgotPassword;
using Duende.AccessTokenManagement;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Identity;

public static class IdentityServiceExtensions
{
    private static readonly ClientCredentialsClientName KeycloakAdminClient =
        ClientCredentialsClientName.Parse("keycloak-admin");

    /// <summary>
    /// Registers the Keycloak Admin API client. Its access token comes from the client
    /// credentials grant and is cached and renewed by Duende.AccessTokenManagement.
    /// </summary>
    public static IServiceCollection AddKeycloakAdministration(this IServiceCollection services)
    {
        services
            .AddOptions<KeycloakAdminOptions>()
            .BindConfiguration(KeycloakAdminOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<KeycloakAdminOptions>,
            KeycloakAdminOptionsValidator
        >();

        services.AddClientCredentialsTokenManagement();

        // The client's settings come from the validated KeycloakAdminOptions (including the
        // secret Aspire supplies), so configuration is bound once, in one place.
        services
            .AddOptions<ClientCredentialsClient>(KeycloakAdminClient.ToString())
            .Configure<IOptions<KeycloakAdminOptions>>(
                (client, keycloak) =>
                {
                    client.TokenEndpoint = keycloak.Value.TokenEndpoint;
                    client.ClientId = ClientId.Parse(keycloak.Value.ClientId);
                    client.ClientSecret = ClientSecret.Parse(keycloak.Value.ClientSecret);
                }
            );

        services
            .AddHttpClient<IPasswordResetService, KeycloakPasswordResetService>(
                (provider, httpClient) =>
                    httpClient.BaseAddress = provider
                        .GetRequiredService<IOptions<KeycloakAdminOptions>>()
                        .Value.BaseAddress
            )
            .AddClientCredentialsTokenHandler(KeycloakAdminClient);

        return services;
    }
}
