using System.Net;
using System.Net.Http.Json;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Infrastructure.Data;
using AppTemplate.Infrastructure.Idempotency;
using AppTemplate.UseCases.Authorization;
using AppTemplate.Web.Features.UserFeatures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppTemplate.FunctionalTests.Idempotency;

/// <summary>
/// Idempotency keys on PUT /v1/users/{id} (ADR 019, Option A: atomic execute and replay),
/// against Postgres. "The handler ran" is measured by counting user UPDATEs.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class IdempotencyTests(IdempotencyTests.ProbeFactory factory)
    : IClassFixture<IdempotencyTests.ProbeFactory>
{
    private static string NewKey() => Guid.NewGuid().ToString();

    private async Task<int> NewUserAsync() =>
        (
            await factory
                .CreateAuthenticatedClient($"sub-{Guid.NewGuid():N}")
                .GetFromJsonAsync<CurrentUserResponse>("/v1/users/me")
        )!.Id;

    private static Task<HttpResponseMessage> PutAsync(
        HttpClient client,
        int id,
        string name,
        string? key
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/v1/users/{id}")
        {
            Content = JsonContent.Create(new { id, name }),
        };
        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }
        return client.SendAsync(request);
    }

    private HttpClient Writer(string? subject = null) =>
        factory.CreateAuthenticatedClient(
            subject ?? $"sub-{Guid.NewGuid():N}",
            Permission.UsersWrite,
            Permission.UsersRead
        );

    private async Task<T> InContextAsync<T>(Func<ApplicationDatabaseContext, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDatabaseContext>());
    }

    private Task<string> NameOfAsync(int id) =>
        InContextAsync(context =>
            context
                .Users.AsNoTracking()
                .Where(u => u.Id == UserId.From(id))
                .Select(u => u.Name.Value)
                .SingleAsync()
        );

    private Task<int> RecordsForKeyAsync(string key) =>
        InContextAsync(context => context.IdempotencyRecords.CountAsync(r => r.Key == key));

    [Fact]
    public async Task Repeat_ReplaysAnIdenticalResponseWithoutRunningAgain()
    {
        var id = await NewUserAsync();
        var client = Writer();
        var key = NewKey();
        var before = factory.Probe.UserUpdates;

        var first = await PutAsync(client, id, "Once Only", key);
        var second = await PutAsync(client, id, "Once Only", key);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(
            await first.Content.ReadAsStringAsync(),
            await second.Content.ReadAsStringAsync()
        );
        Assert.Equal(first.Headers.ETag, second.Headers.ETag);
        Assert.Equal(1, factory.Probe.UserUpdates - before);
        // Nothing ran in between: the replayed ETag is still the current version.
        Assert.Equal(first.Headers.ETag, (await client.GetAsync($"/v1/users/{id}")).Headers.ETag);
    }

    [Fact]
    public async Task SameKeyWithDifferentInputs_Is422()
    {
        var id = await NewUserAsync();
        var client = Writer();
        var key = NewKey();
        (await PutAsync(client, id, "First Body", key)).EnsureSuccessStatusCode();

        var response = await PutAsync(client, id, "Second Body", key);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("First Body", await NameOfAsync(id));
    }

    [Fact]
    public async Task ConcurrentDuplicates_RunOnceAndBothGetTheResult()
    {
        var id = await NewUserAsync();
        var client = Writer();
        var key = NewKey();
        var before = factory.Probe.UserUpdates;
        // Hold the first update open so the duplicate arrives while its transaction is in flight:
        // the duplicate's claim then waits on the unique index instead of failing.
        factory.Probe.DelayNextUserUpdate(TimeSpan.FromMilliseconds(800));

        var first = PutAsync(client, id, "Raced", key);
        await Task.Delay(200);
        var second = PutAsync(client, id, "Raced", key);
        var responses = await Task.WhenAll(first, second);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(
            await responses[0].Content.ReadAsStringAsync(),
            await responses[1].Content.ReadAsStringAsync()
        );
        Assert.Equal(1, factory.Probe.UserUpdates - before);
        Assert.Equal(1, await RecordsForKeyAsync(key));
    }

    [Fact]
    public async Task FailedHandler_LeavesNoRecord_SoARetryRuns()
    {
        var id = await NewUserAsync();
        var client = Writer();
        var key = NewKey();
        factory.Probe.FailNextSave(entries =>
            entries.Any(e => e.Entity is User && e.State == EntityState.Modified)
        );

        var failed = await PutAsync(client, id, "Retried", key);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Equal(0, await RecordsForKeyAsync(key));

        var retried = await PutAsync(client, id, "Retried", key);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Equal("Retried", await NameOfAsync(id));
        Assert.Equal(1, await RecordsForKeyAsync(key));
    }

    [Fact]
    public async Task CrashAfterTheHandlerBeforeCommit_LeavesNeitherTheChangeNorTheRecord()
    {
        var id = await NewUserAsync();
        var original = await NameOfAsync(id);
        var key = NewKey();
        // Fails the save that stores the result, which runs after the handler's own save.
        factory.Probe.FailNextSave(entries =>
            entries.Any(e => e.Entity is IdempotencyRecord && e.State == EntityState.Modified)
        );

        var response = await PutAsync(Writer(), id, "Never Committed", key);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(original, await NameOfAsync(id));
        Assert.Equal(0, await RecordsForKeyAsync(key));
    }

    [Fact]
    public async Task ReplayAfterLosingThePermission_Is403()
    {
        var target = await NewUserAsync();
        var subject = $"sub-{Guid.NewGuid():N}";
        var key = NewKey();
        (await PutAsync(Writer(subject), target, "Admin Rename", key)).EnsureSuccessStatusCode();

        var withoutPermission = factory.CreateAuthenticatedClient(subject); // users:write revoked
        var replay = await PutAsync(withoutPermission, target, "Admin Rename", key);

        Assert.Equal(HttpStatusCode.Forbidden, replay.StatusCode);
    }

    [Fact]
    public async Task SameKeyFromAnotherSubject_RunsIndependently()
    {
        var id = await NewUserAsync();
        var key = NewKey();
        var before = factory.Probe.UserUpdates;

        (await PutAsync(Writer(), id, "Shared Key", key)).EnsureSuccessStatusCode();
        (await PutAsync(Writer(), id, "Shared Key", key)).EnsureSuccessStatusCode();

        Assert.Equal(2, factory.Probe.UserUpdates - before);
        Assert.Equal(2, await RecordsForKeyAsync(key));
    }

    [Theory]
    [InlineData("has spaces")]
    [InlineData("ünïcode")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123456789")]
    public async Task MalformedKey_Is400(string key)
    {
        var id = await NewUserAsync();

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await PutAsync(Writer(), id, "Bad Key", key)).StatusCode
        );
    }

    [Fact]
    public async Task CleanupJob_DeletesExpiredRecordsOnly()
    {
        var (expired, live) = (NewKey(), NewKey());
        await InContextAsync(async context =>
        {
            foreach (
                var (key, expiresIn) in new[]
                {
                    (expired, TimeSpan.FromHours(-1)),
                    (live, TimeSpan.FromHours(1)),
                }
            )
            {
                context.IdempotencyRecords.Add(
                    new IdempotencyRecord
                    {
                        Id = Guid.CreateVersion7(),
                        Subject = "sub-cleanup",
                        Operation = "users.update.v1",
                        Key = key,
                        Fingerprint = new string('0', 64),
                        CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                        ExpiresAtUtc = DateTimeOffset.UtcNow + expiresIn,
                    }
                );
            }
            return await context.SaveChangesAsync();
        });

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope
                .ServiceProvider.GetRequiredService<IdempotencyCleanupJob>()
                .ExecuteAsync(CancellationToken.None);
        }

        Assert.Equal(0, await RecordsForKeyAsync(expired));
        Assert.Equal(1, await RecordsForKeyAsync(live));
    }

    /// <summary>Counts user UPDATEs sent to the database, and can delay or fail one chosen save.</summary>
    public sealed class SaveProbe : SaveChangesInterceptor
    {
        private readonly Lock _lock = new();
        private int _userUpdates;
        private TimeSpan? _delay;
        private Func<IReadOnlyList<EntityEntry>, bool>? _failWhen;

        public int UserUpdates => Volatile.Read(ref _userUpdates);

        public void DelayNextUserUpdate(TimeSpan delay)
        {
            lock (_lock)
            {
                _delay = delay;
            }
        }

        public void FailNextSave(Func<IReadOnlyList<EntityEntry>, bool> when)
        {
            lock (_lock)
            {
                _failWhen = when;
            }
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            var entries = eventData.Context!.ChangeTracker.Entries().ToList();
            var updatesUser = entries.Any(e => e.Entity is User && e.State == EntityState.Modified);
            TimeSpan? delay = null;
            lock (_lock)
            {
                if (_failWhen is { } failWhen && failWhen(entries))
                {
                    _failWhen = null;
                    throw new InvalidOperationException("Simulated failure");
                }
                if (updatesUser)
                {
                    (delay, _delay) = (_delay, null);
                }
            }
            if (updatesUser)
            {
                Interlocked.Increment(ref _userUpdates);
                if (delay is { } wait)
                {
                    await Task.Delay(wait, cancellationToken);
                }
            }
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    public sealed class ProbeFactory : AppTemplateWebApplicationFactory
    {
        public SaveProbe Probe { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
                services.AddSingleton<ISaveChangesInterceptor>(Probe)
            );
        }
    }
}
