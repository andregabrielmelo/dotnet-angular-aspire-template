/// <summary>
/// Third-party sign-in (Google, GitHub, Microsoft) brokered by Keycloak. A provider is switched
/// on only when its client id is configured - in the AppHost's user secrets, for example:
/// <code>
/// dotnet user-secrets set "Parameters:google-client-id" "..."
/// dotnet user-secrets set "Parameters:google-client-secret" "..."
/// </code>
/// The realm file reads these through ${KC_IDP_*} placeholders when Keycloak imports it, and
/// the backend for frontend is told which providers to offer. The OAuth app registered at each
/// provider must allow the redirect URI
/// http://localhost:8080/realms/apptemplate/broker/{alias}/endpoint.
/// </summary>
internal static class ExternalIdentityProviders
{
    // Aliases must match the identityProviders entries in Realms/apptemplate-realm.json.
    private static readonly string[] Aliases = ["google", "github", "microsoft"];

    public static void Configure(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<KeycloakResource> keycloak,
        IResourceBuilder<ProjectResource> backendForFrontend
    )
    {
        foreach (var alias in Aliases)
        {
            var variablePrefix = $"KC_IDP_{alias.ToUpperInvariant()}";
            var clientId = builder.Configuration[$"Parameters:{alias}-client-id"];
            var enabled = !string.IsNullOrWhiteSpace(clientId);

            keycloak
                .WithEnvironment($"{variablePrefix}_ENABLED", enabled ? "true" : "false")
                // Keycloak rejects empty client credentials even for disabled providers.
                .WithEnvironment(
                    $"{variablePrefix}_CLIENT_ID",
                    enabled ? clientId : "not-configured"
                );

            if (enabled)
            {
                var clientSecret = builder.AddParameter($"{alias}-client-secret", secret: true);
                keycloak.WithEnvironment($"{variablePrefix}_CLIENT_SECRET", clientSecret);
            }
            else
            {
                keycloak.WithEnvironment($"{variablePrefix}_CLIENT_SECRET", "not-configured");
            }

            backendForFrontend.WithEnvironment(
                $"ExternalIdentityProviders__{alias}__Enabled",
                enabled ? "true" : "false"
            );
        }
    }
}
