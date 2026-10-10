using AppTemplate.BackendForFrontend.Configurations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AppTemplate.BackendForFrontend.Tests;

/// <summary>
/// The backend for frontend's own options: registered and validated, and a missing required
/// Keycloak setting stops startup naming the key instead of falling back to a literal.
/// </summary>
public class ConfigurationTests(BackendForFrontendFactory factory)
    : IClassFixture<BackendForFrontendFactory>
{
    [Fact]
    public void ExternalIdentityProviders_AreRegisteredAndValidated()
    {
        Assert.NotEmpty(
            factory.Services.GetServices<IConfigureOptions<ExternalIdentityProvidersOptions>>()
        );
        Assert.NotEmpty(
            factory.Services.GetServices<IValidateOptions<ExternalIdentityProvidersOptions>>()
        );
    }

    [Theory]
    [InlineData("Keycloak:Realm")]
    [InlineData("Keycloak:ClientId")]
    public void MissingRequiredKeycloakSetting_FailsStartupNamingTheKey(string key)
    {
        using var misconfigured = new BackendForFrontendFactory().WithWebHostBuilder(builder =>
            builder.UseSetting(key, "")
        );

        var exception = Assert.ThrowsAny<Exception>(() => misconfigured.Services);

        Assert.Contains(key, exception.ToString());
    }
}
