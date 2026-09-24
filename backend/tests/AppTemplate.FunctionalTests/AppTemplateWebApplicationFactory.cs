using AppTemplate.Infrastructure.Data;
using AppTemplate.UseCases.Users.ForgotPassword;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AppTemplate.FunctionalTests;

/// <summary>
/// Swaps the real Postgres-backed DbContext for an isolated in-memory one, so functional
/// tests exercise the full HTTP -> FastEndpoints -> Mediator -> EF Core pipeline without
/// needing a real database. "Testing" environment keeps Program.cs from running real
/// migrations against it (EF Core's InMemory provider doesn't support them).
/// Keycloak JWT validation is replaced by <see cref="TestAuthHandler"/> - use
/// <see cref="CreateAuthenticatedClient"/> for calls that need a signed-in user.
/// </summary>
public class AppTemplateWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"AppTemplateFunctionalTests-{Guid.NewGuid()}";

    /// <summary>Replaces the Keycloak Admin API client; inspect or reconfigure it per test.</summary>
    public FakePasswordResetService PasswordResetService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // AddInfrastructureServices reads ConnectionStrings:apptemplate and throws if it's
        // missing, before ConfigureServices below gets a chance to replace the DbContext -
        // supply a placeholder so that guard passes; it's never actually connected to.
        builder.UseSetting("ConnectionStrings:apptemplate", "Host=unused;Database=unused");
        // KeycloakAdminOptions is validated on start; the real client is replaced below.
        builder.UseSetting("Keycloak:Admin:ClientSecret", "functional-tests");
        builder.UseSetting("Keycloak:Authority", "https://keycloak.test/realms/apptemplate");

        builder.ConfigureServices(services =>
        {
            // Removing just DbContextOptions<T> leaves EF Core's internal per-provider
            // service registrations from the original AddDbContext call behind, which then
            // conflicts with the ones InMemory registers below ("Only a single database
            // provider can be registered..."). Strip everything EF Core registered and
            // start clean.
            var efCoreDescriptors = services
                .Where(d =>
                    d.ServiceType.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true
                )
                .ToList();
            foreach (var descriptor in efCoreDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDatabaseContext>(options =>
                options.UseInMemoryDatabase(_databaseName)
            );

            services.RemoveAll<IPasswordResetService>();
            services.AddSingleton<IPasswordResetService>(PasswordResetService);

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

    /// <summary>
    /// A client whose requests are authenticated as the given <c>sub</c>, holding the given
    /// API permissions (Keycloak client roles).
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string subject, params string[] permissions)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, subject);
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
