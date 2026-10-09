using AppTemplate.Core.Interfaces;
using AppTemplate.FunctionalTests.Jobs;
using AppTemplate.Infrastructure.Data;
using AppTemplate.Infrastructure.Jobs.Extensions;
using AppTemplate.UseCases.Users.ForgotPassword;
using Hangfire;
using Hangfire.InMemory;
using Hangfire.Logging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AppTemplate.FunctionalTests;

/// <summary>
/// Hosts the real API - HTTP -> FastEndpoints -> Mediator -> EF Core -> Postgres - against a
/// fresh database in a shared Postgres container (see <see cref="PostgresTestDatabase"/>), so
/// unique indexes, database defaults, raw SQL and owned types behave as in production. Needs
/// Docker: mark every test class that uses this factory with
/// <c>[Trait(TestCategories.Name, TestCategories.RequiresDocker)]</c>.
/// Keycloak JWT validation is replaced by <see cref="TestAuthHandler"/> - use
/// <see cref="CreateAuthenticatedClient"/> for calls that need a signed-in user.
/// </summary>
public class AppTemplateWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>The app's Postgres connection string, set by <see cref="InitializeAsync"/>.</summary>
    protected string? ConnectionString { get; set; }

    /// <summary>Replaces the Keycloak Admin API client; inspect or reconfigure it per test.</summary>
    public FakePasswordResetService PasswordResetService { get; } = new();

    /// <summary>Counts runs of <see cref="TestRecurringJobDefinition"/>.</summary>
    public TestRecurringJobProbe TestRecurringJob { get; } = new();

    /// <summary>Records emails instead of sending them over SMTP.</summary>
    public FakeEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting(
            "ConnectionStrings:apptemplate",
            ConnectionString
                ?? throw new InvalidOperationException(
                    "The factory is used before InitializeAsync - use it as an xUnit fixture."
                )
        );
        // KeycloakAdminOptions is validated on start; the real client is replaced below.
        builder.UseSetting("Keycloak:Admin:ClientSecret", "functional-tests");
        builder.UseSetting("Keycloak:Authority", "https://keycloak.test/realms/apptemplate");
        // Jobs are enqueued into in-memory storage (below) but never executed in the
        // background - tests run job classes directly when they need to.
        builder.UseSetting("JobScheduling:RunServer", "false");
        // Every request arrives from a simulated backend for frontend on loopback (see
        // TestPeerAddress), the one trusted proxy, so each client's X-Forwarded-For is honored.
        builder.UseSetting("ForwardedHeaders:KnownProxies:0", TestPeerAddress.TrustedProxy);
        // Far above anything a test sends; RateLimitingTests lowers them in its own host.
        builder.UseSetting("RateLimiting:AnonymousPermitLimit", "100000");
        builder.UseSetting("RateLimiting:AuthenticatedPermitLimit", "100000");

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestPeerAddress>();

            services.RemoveAll<IPasswordResetService>();
            services.AddSingleton<IPasswordResetService>(PasswordResetService);

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            // Hangfire's log provider is global: keep it off any single test host's
            // (disposable) ILoggerFactory, since hosts run in parallel.
            services.AddSingleton<ILogProvider>(NoOpHangfireLogProvider.Instance);

            // One isolated store per test host (never Hangfire's global JobStorage.Current),
            // created lazily so it picks up the log provider above.
            services.RemoveAll<JobStorage>();
            services.AddSingleton<JobStorage>(_ => new InMemoryStorage());

            // A recurring job the tests control, so they never have to run (or pause) real ones.
            services.AddSingleton(TestRecurringJob);
            services.AddRecurringJob<TestRecurringJobDefinition>();

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { }
                );
        });
    }

    /// <summary>Creates this host's database (by running the migrations) before any test uses it.</summary>
    public virtual async Task InitializeAsync()
    {
        ConnectionString = await PostgresTestDatabase.NewDatabaseConnectionStringAsync();

        using var scope = Services.CreateScope();
        await scope
            .ServiceProvider.GetRequiredService<ApplicationDatabaseContext>()
            .Database.MigrateAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    private static int _nextClientAddress;

    /// <summary>
    /// A client whose requests are authenticated as the given <c>sub</c>, holding the given
    /// API permissions (Keycloak client roles).
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string subject, params string[] permissions)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, subject);
        // Anonymous rate limits key on the client address (as forwarded by the trusted
        // backend for frontend) - every client gets its own, so tests never share a window.
        var n = Interlocked.Increment(ref _nextClientAddress);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.1.{n / 250}.{n % 250 + 1}");
        if (permissions.Length > 0)
        {
            client.DefaultRequestHeaders.Add(
                TestAuthHandler.PermissionsHeader,
                string.Join(',', permissions)
            );
        }
        return client;
    }
}
