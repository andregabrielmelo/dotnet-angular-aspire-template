using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Identity;

/// <summary>
/// Settings for calling Keycloak's Admin REST API with a service-account client
/// (client credentials grant). Bound from the <c>Keycloak:Admin</c> configuration section.
/// </summary>
public sealed class KeycloakAdminOptions
{
    public const string SectionName = "Keycloak:Admin";

    /// <summary>Aspire service-discovery address of the Keycloak resource.</summary>
    [Required]
    public Uri BaseAddress { get; set; } = new("https+http://keycloak");

    [Required]
    public string Realm { get; set; } = "apptemplate";

    /// <summary>Confidential client with realm-management's view-users and manage-users roles.</summary>
    [Required]
    public string ClientId { get; set; } = "apptemplate-user-admin";

    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Client the emailed reset link belongs to; <see cref="PasswordResetRedirectUri"/> must be valid for it.</summary>
    [Required]
    public string PasswordResetClientId { get; set; } = "apptemplate-backend-for-frontend";

    /// <summary>Where Keycloak sends the user after the new password has been set.</summary>
    [Required]
    public Uri PasswordResetRedirectUri { get; set; } = new("https://localhost:7100/auth");

    /// <summary>How long the emailed link stays valid.</summary>
    [Range(typeof(TimeSpan), "00:05:00", "1.00:00:00")]
    public TimeSpan PasswordResetLinkLifespan { get; set; } = TimeSpan.FromMinutes(30);

    public Uri TokenEndpoint =>
        new(BaseAddress, $"realms/{Uri.EscapeDataString(Realm)}/protocol/openid-connect/token");
}

/// <summary>Source-generated validator for the data annotations on <see cref="KeycloakAdminOptions"/>.</summary>
[OptionsValidator]
public sealed partial class KeycloakAdminOptionsValidator : IValidateOptions<KeycloakAdminOptions>;
