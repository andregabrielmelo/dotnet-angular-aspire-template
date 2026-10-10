namespace AppTemplate.Infrastructure.Identity;

/// <summary>
/// A user as Keycloak's Admin API returns it (<c>briefRepresentation=true</c>): only the
/// fields the profile sync reads.
/// </summary>
internal sealed record KeycloakUser(
    string Id,
    string? Username,
    string? Email,
    string? FirstName,
    string? LastName
)
{
    /// <summary>"First Last" when either is set, otherwise the username.</summary>
    public string? GetDisplayName()
    {
        var fullName = $"{FirstName} {LastName}".Trim();
        return fullName.Length > 0 ? fullName : Username;
    }
}
