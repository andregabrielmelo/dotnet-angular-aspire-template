using AppTemplate.Infrastructure.Auditing;
using AppTemplate.Infrastructure.Email;
using AppTemplate.Infrastructure.Files;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.Infrastructure.Jobs.Options;
using AppTemplate.Infrastructure.Outbox;
using AppTemplate.UseCases.Files;
using AppTemplate.UseCases.Users.SendWelcomeEmail;
using AppTemplate.Web.Authorization;
using AppTemplate.Web.Configurations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Xunit;

namespace AppTemplate.FunctionalTests.Configuration;

/// <summary>
/// The Web host's options are registered before use and never invented at a call site
/// (AGENTS.md, Configuration). This pins the options this composition root needs, so dropping
/// a registration fails here instead of silently using defaults.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class OptionsRegistrationTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    public static TheoryData<Type> WebOptions() =>
        [
            typeof(FileStorageOptions),
            typeof(JobSchedulingOptions),
            typeof(AuditOptions),
            typeof(OutboxOptions),
            typeof(KeycloakAdminOptions),
            typeof(KeycloakAuthorizationOptions),
            typeof(MailserverConfiguration),
            typeof(WelcomeEmailOptions),
            typeof(ForwardedHeadersSettings),
            typeof(RateLimitingSettings),
            typeof(RequestTimeoutSettings),
        ];

    [Theory]
    [MemberData(nameof(WebOptions))]
    public void Options_AreRegistered(Type options)
    {
        var configurations = factory.Services.GetServices(
            typeof(IConfigureOptions<>).MakeGenericType(options)
        );

        Assert.NotEmpty(configurations);
    }

    [Theory]
    [MemberData(nameof(WebOptions))]
    public void Options_AreValidated(Type options)
    {
        // KeycloakAuthorizationOptions has nothing left to validate: its only value comes
        // from the required Keycloak:Audience, which fails startup when missing.
        if (options == typeof(KeycloakAuthorizationOptions))
        {
            return;
        }

        var validators = factory.Services.GetServices(
            typeof(IValidateOptions<>).MakeGenericType(options)
        );

        Assert.NotEmpty(validators);
    }

    [Fact]
    public async Task EmptyFileStorage_StartsWithStorageOff()
    {
        // The test host sets no FileStorage section: storage is an optional feature.
        Assert.Equal(
            "UnconfiguredFileStorage",
            factory.Services.GetRequiredService<IFileStorage>().GetType().Name
        );
        var report = await factory
            .Services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(registration => registration.Name == "file-storage");
        Assert.Empty(report.Entries);
    }
}

/// <summary>A required setting that's missing, or an optional feature configured halfway, stops startup.</summary>
public class StartupConfigurationTests
{
    [Theory]
    [InlineData("Keycloak:Realm")]
    [InlineData("Keycloak:Audience")]
    public void MissingRequiredKeycloakSetting_FailsStartupNamingTheKey(string key)
    {
        using var factory = new MisconfiguredFactory((key, ""));

        var exception = Assert.ThrowsAny<Exception>(() => factory.Services);

        Assert.Contains(key, Flatten(exception));
    }

    [Fact]
    public void PartialFileStorage_FailsStartup()
    {
        // An endpoint without credentials must not quietly become "storage off".
        using var factory = new MisconfiguredFactory(
            ("FileStorage:ServiceUrl", "http://127.0.0.1:3900")
        );

        var exception = Assert.ThrowsAny<Exception>(() => factory.Services);

        Assert.Contains("FileStorage needs an absolute ServiceUrl", Flatten(exception));
    }

    private static string Flatten(Exception exception) =>
        exception.InnerException is null
            ? exception.ToString()
            : exception + Flatten(exception.InnerException);

    /// <summary>
    /// The real host with some settings overridden. It fails before touching the database,
    /// so it never needs one; a placeholder connection string satisfies the base factory.
    /// </summary>
    private sealed class MisconfiguredFactory : AppTemplateWebApplicationFactory
    {
        private readonly (string Key, string Value)[] _settings;

        public MisconfiguredFactory(params (string Key, string Value)[] settings)
        {
            _settings = settings;
            ConnectionString = "Host=127.0.0.1;Port=1;Database=unused";
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            foreach (var (key, value) in _settings)
            {
                builder.UseSetting(key, value);
            }
        }
    }
}
