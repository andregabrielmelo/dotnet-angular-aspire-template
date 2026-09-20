namespace AppTemplate.Web.Configurations.Auth;

/// <summary>Bound from "Authentication:Google". ClientSecret is a secret - set via
/// user-secrets/environment variables, never committed.</summary>
public class GoogleAuthOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
