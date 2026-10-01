using System.Net;
using System.Net.Http.Json;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.FunctionalTests.UserFeatures;
using AppTemplate.Infrastructure.Data;
using AppTemplate.Infrastructure.Jobs;
using AppTemplate.Infrastructure.Jobs.FireAndForget;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Users;
using AppTemplate.UseCases.Users.List;
using AppTemplate.Web.Features.UserFeatures;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppTemplate.FunctionalTests.Data;

/// <summary>
/// Behavior that only a real Postgres has - the reason these tests don't use an in-memory
/// database: unique indexes, database-generated values, owned types and raw SQL.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class PostgresPersistenceTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    private static string NewSubject() => $"sub-{Guid.NewGuid():N}";

    private static User NewUser(string externalId, string? email = null) =>
        User.Create(
            externalId,
            UserName.From("Ada Lovelace"),
            new EmailAddress(email ?? $"{externalId}@example.com")
        );

    private async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    private Task<User> AddAsync(User user) =>
        InScopeAsync(services => services.GetRequiredService<IRepository<User>>().AddAsync(user));

    private Task<User?> LoadAsync(UserId id) =>
        InScopeAsync(services =>
            services
                .GetRequiredService<ApplicationDatabaseContext>()
                .Users.AsNoTracking()
                .SingleOrDefaultAsync(user => user.Id == id)
        );

    [Fact]
    public async Task Insert_WithDuplicateExternalId_ThrowsUniqueConstraintViolation()
    {
        var externalId = NewSubject();
        await AddAsync(NewUser(externalId));

        var ex = await Assert.ThrowsAsync<UniqueConstraintViolationException>(() =>
            AddAsync(NewUser(externalId, email: $"other-{externalId}@example.com"))
        );
        Assert.Contains("external_id", ex.ConstraintName);
    }

    [Fact]
    public async Task Insert_WithDuplicateEmail_ThrowsUniqueConstraintViolation()
    {
        var email = $"{NewSubject()}@example.com";
        await AddAsync(NewUser(NewSubject(), email));

        var ex = await Assert.ThrowsAsync<UniqueConstraintViolationException>(() =>
            AddAsync(NewUser(NewSubject(), email))
        );
        Assert.Contains("email", ex.ConstraintName);
    }

    [Fact]
    public async Task Insert_SetsCreatedAtUtcFromTheDatabaseClock()
    {
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        var user = await AddAsync(NewUser(NewSubject()));
        var stored = await LoadAsync(user.Id);

        Assert.InRange(stored!.CreatedAtUtc, before, DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task PhoneNumber_RoundTripsAsAnOwnedType()
    {
        var user = NewUser(NewSubject());
        user.UpdatePhoneNumber(new PhoneNumber("+55", "11 98765 4321", "12"));

        var saved = await AddAsync(user);
        var stored = await LoadAsync(saved.Id);

        Assert.Equal(new PhoneNumber("+55", "11 98765 4321", "12"), stored!.PhoneNumber);
    }

    [Fact]
    public async Task ListUsersQueryService_RawSqlMatchesTheSchema()
    {
        // FromSqlRaw must list every mapped column by its snake_case name - a renamed or added
        // column breaks it at runtime only, so run it for real.
        var user = NewUser(NewSubject());
        user.UpdatePhoneNumber(new PhoneNumber("+1", "555 0100", null));
        var saved = await AddAsync(user);

        var page = await InScopeAsync(services =>
            services.GetRequiredService<IListUsersQueryService>().ListAsync(1, 1000, default)
        );

        var listed = Assert.Single(page.Items, item => item.Id == saved.Id);
        Assert.Equal(user.PhoneNumber, listed.PhoneNumber);
    }

    [Fact]
    public async Task ListEndpoint_ReturnsProvisionedUsers()
    {
        var me = await factory.CreateAuthenticatedClient(NewSubject()).ProvisionMeAsync();

        var response = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersRead)
            .GetFromJsonAsync<UserPage>($"/users?page=1&per_page={Constants.MAX_PAGE_SIZE}");

        Assert.Contains(response!.Items, item => item.Id == me.Id);
    }

    private sealed record UserPage(List<UserRecord> Items);

    [Fact]
    public async Task ConcurrentProvisioning_OfTheSameIdentity_CreatesOneUser()
    {
        // e.g. the SPA open in two tabs, or a retried request, on a first sign-in.
        var subject = NewSubject();
        var clients = Enumerable
            .Range(0, 8)
            .Select(_ => factory.CreateAuthenticatedClient(subject))
            .ToList();

        var responses = await Task.WhenAll(
            clients.Select(client => client.PostAsync("/users/me", content: null))
        );

        Assert.All(
            responses,
            response =>
                Assert.True(
                    response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created,
                    $"Expected 200 or 201, got {(int)response.StatusCode}: "
                        + response.Content.ReadAsStringAsync().Result
                )
        );
        var users = await Task.WhenAll(
            responses.Select(response => response.Content.ReadFromJsonAsync<CurrentUserResponse>())
        );
        Assert.Single(users.Select(user => user!.Id).Distinct());

        var rows = await InScopeAsync(services =>
            services
                .GetRequiredService<ApplicationDatabaseContext>()
                .Users.CountAsync(user => user.ExternalId == subject)
        );
        Assert.Equal(1, rows);

        // Only the request that actually inserted raised UserCreatedEvent.
        var welcomeEmails = factory
            .Services.GetRequiredService<JobStorage>()
            .GetMonitoringApi()
            .EnqueuedJobs(JobQueues.Critical, 0, 1000)
            .Count(job =>
                job.Value.Job.Type == typeof(WelcomeEmailJob)
                && (int)job.Value.Job.Args[0] == users[0]!.Id
            );
        Assert.Equal(1, welcomeEmails);
    }
}
