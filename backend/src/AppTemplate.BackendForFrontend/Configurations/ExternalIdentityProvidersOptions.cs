using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace AppTemplate.BackendForFrontend.Configurations;

/// <summary>
/// Third-party sign-in providers (Google, GitHub, Microsoft, ...) brokered by Keycloak, keyed
/// by their Keycloak identity-provider alias. Only <see cref="ExternalIdentityProviderOptions.Enabled"/>
/// ones are offered to users; the AppHost enables a provider when its credentials are configured.
/// </summary>
public sealed class ExternalIdentityProvidersOptions
{
    public const string SectionName = "ExternalIdentityProviders";

    public Dictionary<string, ExternalIdentityProviderOptions> Providers { get; set; } =
        new(StringComparer.Ordinal);

    public bool IsEnabled(string alias) =>
        Providers.TryGetValue(alias, out var provider) && provider.Enabled;
}

public sealed class ExternalIdentityProviderOptions
{
    public string DisplayName { get; set; } = string.Empty;

    public bool Enabled { get; set; }
}

public sealed partial class ExternalIdentityProvidersOptionsValidator
    : IValidateOptions<ExternalIdentityProvidersOptions>
{
    public ValidateOptionsResult Validate(string? name, ExternalIdentityProvidersOptions options)
    {
        var failures = new List<string>();

        foreach (var (alias, provider) in options.Providers)
        {
            // The alias ends up in a query string sent to Keycloak - keep it to a safe shape.
            if (!AliasPattern().IsMatch(alias))
            {
                failures.Add($"Identity provider alias '{alias}' must match {AliasPattern()}.");
            }

            if (string.IsNullOrWhiteSpace(provider.DisplayName))
            {
                failures.Add($"Identity provider '{alias}' needs a DisplayName.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,31}$")]
    public static partial Regex AliasPattern();
}
