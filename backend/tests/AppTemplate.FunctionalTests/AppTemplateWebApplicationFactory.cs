using AppTemplate.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppTemplate.FunctionalTests;

/// <summary>
/// Swaps the real Postgres-backed DbContext for an isolated in-memory one, so functional
/// tests exercise the full HTTP -> FastEndpoints -> Mediator -> EF Core pipeline without
/// needing a real database. "Testing" environment keeps Program.cs from running real
/// migrations against it (EF Core's InMemory provider doesn't support them).
/// </summary>
public class AppTemplateWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"AppTemplateFunctionalTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // AddInfrastructureServices reads ConnectionStrings:apptemplate and throws if it's
        // missing, before ConfigureServices below gets a chance to replace the DbContext -
        // supply a placeholder so that guard passes; it's never actually connected to.
        builder.UseSetting("ConnectionStrings:apptemplate", "Host=unused;Database=unused");

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
        });
    }
}
