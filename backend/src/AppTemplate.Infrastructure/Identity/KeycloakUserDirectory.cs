using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Identity;

/// <summary>
/// Pages through Keycloak's Admin API user list as the user-admin service account (the same
/// token handler as <see cref="KeycloakPasswordResetService"/>; needs realm-management's
/// view-users role).
/// </summary>
public sealed class KeycloakUserDirectory(
    HttpClient httpClient,
    IOptions<KeycloakAdminOptions> options
) : IIdentityProviderDirectory
{
    public const int PageSize = 100;
    private const string ServiceAccountPrefix = "service-account-";

    public async IAsyncEnumerable<IdentityProviderUser> ListUsersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var realm = Uri.EscapeDataString(options.Value.Realm);

        for (var first = 0; ; first += PageSize)
        {
            var page =
                await httpClient.GetFromJsonAsync<KeycloakUser[]>(
                    $"admin/realms/{realm}/users?first={first}&max={PageSize}&briefRepresentation=true",
                    cancellationToken
                ) ?? [];

            foreach (var user in page)
            {
                if (
                    user.Username?.StartsWith(ServiceAccountPrefix, StringComparison.Ordinal)
                    == true
                )
                {
                    continue;
                }

                yield return new IdentityProviderUser(user.Id, user.GetDisplayName(), user.Email);
            }

            if (page.Length < PageSize)
            {
                yield break;
            }
        }
    }
}
