using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Playwright;
using Xunit;

namespace AppTemplate.EndToEndTests;

/// <summary>Trait for tests that start the whole AppHost: CI runs them only in e2e.yml.</summary>
public static class TestCategories
{
    public const string Name = "Category";
    public const string RequiresFullStack = "RequiresFullStack";
}

/// <summary>
/// Starts the real AppHost once for all end-to-end tests: Postgres, Redis, Keycloak, Mailpit,
/// the API, the Angular dev server and the backend for frontend, exactly as
/// <c>dotnet run --project src/AppTemplate.AppHost</c> does. Data volumes and persistent
/// lifetimes are removed, so every run starts from an empty database and a freshly imported
/// realm and leaves nothing behind. Then it opens one Chromium for the tests to share.
/// </summary>
public sealed class FullStackFixture : IAsyncLifetime
{
    /// <summary>Pinned in AppHost.cs: the realm's redirect URIs point at it.</summary>
    public const string BaseUrl = "https://localhost:7100";

    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(10);

    private DistributedApplication? _app;
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        var builder =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppTemplate_AppHost>([
                // The AppHost's dev parameters (client secrets) live in appsettings.Development.json.
                "--environment=Development",
                // Keep the pinned ports (7100, 8080): the realm and the issuer URL depend on them.
                "--DcpPublisher:RandomizePorts=false",
            ]);

        foreach (var container in builder.Resources.OfType<ContainerResource>())
        {
            foreach (
                var annotation in container
                    .Annotations.Where(a =>
                        a is ContainerMountAnnotation or ContainerLifetimeAnnotation
                    )
                    .ToList()
            )
            {
                container.Annotations.Remove(annotation);
            }
        }

        _app = await builder.BuildAsync();
        using var startup = new CancellationTokenSource(StartupTimeout);
        await _app.StartAsync(startup.Token);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync(
            "backend-for-frontend",
            startup.Token
        );

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync();
    }

    /// <summary>
    /// A fresh browser profile (no cookies). The backend for frontend uses the ASP.NET Core
    /// development certificate, which a CI runner doesn't trust. Traces are kept for failures.
    /// </summary>
    public async Task<IBrowserContext> NewContextAsync()
    {
        var context = await Browser.NewContextAsync(
            new BrowserNewContextOptions { BaseURL = BaseUrl, IgnoreHTTPSErrors = true }
        );
        await context.Tracing.StartAsync(
            new TracingStartOptions { Screenshots = true, Snapshots = true }
        );
        return context;
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }
        _playwright?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class FullStackCollection : ICollectionFixture<FullStackFixture>
{
    public const string Name = "Full stack";
}
