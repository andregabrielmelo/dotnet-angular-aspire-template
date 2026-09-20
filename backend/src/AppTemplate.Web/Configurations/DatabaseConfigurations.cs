using AppTemplate.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AppTemplate.Web.Configurations;

public static class DatabaseConfigurations
{
    public static async Task<IApplicationBuilder> StartDatabase(this WebApplication app)
    {
        // Run migrations and seed in Developme nt or when explicitly requested via environment variable
        var shouldMigrate =
            app.Environment.IsDevelopment()
            || app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");

        if (shouldMigrate)
        {
            await MigrateDatabaseAsync(app);
            await SeedDatabaseAsync(app);
        }

        return app;
    }

    static async Task MigrateDatabaseAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();
        var context = services.GetRequiredService<ApplicationDatabaseContext>();

        try
        {
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                logger.LogInformation("Applying database migrations...");
                foreach (var migration in pendingMigrations)
                {
                    logger.LogInformation("Pending migration: {migration}", migration);
                }

                await context.Database.MigrateAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "An error occurred migrating the DB. {exceptionMessage}",
                ex.Message
            );
            throw; // Re-throw to make startup fail if migrations fail
        }
    }

    static async Task SeedDatabaseAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();

        try
        {
            logger.LogInformation("Seeding database...");
            var context = services.GetRequiredService<ApplicationDatabaseContext>();
            await SeedData.InitializeAsync(context);
            logger.LogInformation("Database seeded successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred seeding the DB. {exceptionMessage}", ex.Message);
            // Don't re-throw for seeding errors - it's not critical
        }
    }
}
