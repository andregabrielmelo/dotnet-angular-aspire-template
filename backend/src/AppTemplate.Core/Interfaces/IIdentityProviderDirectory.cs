namespace AppTemplate.Core.Interfaces;

/// <summary>A user as the identity provider (Keycloak) knows them.</summary>
public sealed record IdentityProviderUser(string Id, string? Name, string? Email);

/// <summary>
/// Reads users from the identity provider. Implemented in Infrastructure.
/// Implemented by <c>KeycloakUserDirectory</c>. Used by <c>SyncUserProfilesHandler</c>.
/// </summary>
public interface IIdentityProviderDirectory
{
    /// <summary>All regular users (no service accounts), fetched page by page.</summary>
    IAsyncEnumerable<IdentityProviderUser> ListUsersAsync(CancellationToken cancellationToken);
}
