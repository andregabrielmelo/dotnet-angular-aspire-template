using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.Infrastructure.Data;

public class SeedData
{
    public const int NUMBER_OF_SEED_USERS = 25;

    public static async Task InitializeAsync(ApplicationDatabaseContext dbContext)
    {
        if (await dbContext.DomainUsers.AnyAsync())
            return; // DB has been seeded

        await PopulateTestDataAsync(dbContext);
    }

    public static async Task PopulateTestDataAsync(ApplicationDatabaseContext dbContext)
    {
        var names = new List<string>();
        for (int i = 1; i <= NUMBER_OF_SEED_USERS; i++)
        {
            names.Add($"User {i}");
        }

        // Seed users are fixtures for pagination demos, not real accounts - they have no
        // Identity credentials and are not meant to be logged into.
        var users = names.Select(name => new User(
            UserName.From(name),
            new EmailAddress($"{name.ToLowerInvariant().Replace(" ", "")}@example.com")
        ));
        dbContext.DomainUsers.AddRange(users);
        await dbContext.SaveChangesAsync();
    }
}
