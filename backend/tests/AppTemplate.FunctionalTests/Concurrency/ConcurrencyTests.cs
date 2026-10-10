using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Authorization;
using AppTemplate.Web.Features.UserFeatures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppTemplate.FunctionalTests.Concurrency;

/// <summary>
/// ETags and If-Match on users (ADR 019): the ETag is the row's xmin, If-Match is parsed per
/// RFC 9110, and the check is atomic with the UPDATE, so of two racing writes exactly one wins.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class ConcurrencyTests(ConcurrencyTests.RaceFactory factory)
    : IClassFixture<ConcurrencyTests.RaceFactory>
{
    private HttpClient Admin() =>
        factory.CreateAuthenticatedClient(
            $"sub-{Guid.NewGuid():N}",
            Permission.UsersRead,
            Permission.UsersWrite
        );

    private async Task<(int Id, EntityTagHeaderValue ETag)> NewUserAsync()
    {
        var me = (
            await factory
                .CreateAuthenticatedClient($"sub-{Guid.NewGuid():N}")
                .GetFromJsonAsync<CurrentUserResponse>("/v1/users/me")
        )!;
        var response = await Admin().GetAsync($"/v1/users/{me.Id}");
        response.EnsureSuccessStatusCode();
        return (me.Id, response.Headers.ETag!);
    }

    private Task<HttpResponseMessage> PutAsync(
        int id,
        string name,
        string? ifMatch,
        HttpClient? client = null
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/v1/users/{id}")
        {
            Content = JsonContent.Create(new { id, name }),
        };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }
        return (client ?? Admin()).SendAsync(request);
    }

    private async Task<string> NameOfAsync(int id) =>
        (await Admin().GetFromJsonAsync<JsonElement>($"/v1/users/{id}"))
            .GetProperty("name")
            .GetString()!;

    [Fact]
    public async Task Get_ReturnsAQuotedStrongETag()
    {
        var (_, etag) = await NewUserAsync();

        Assert.False(etag.IsWeak);
        Assert.Matches("^\"[0-9]+\"$", etag.Tag);
    }

    [Fact]
    public async Task WithoutIfMatch_TheUpdateSucceedsAndTheETagChanges()
    {
        var (id, etag) = await NewUserAsync();

        var response = await PutAsync(id, "No Precondition", ifMatch: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(etag, response.Headers.ETag);
    }

    [Fact]
    public async Task MatchingIfMatch_Updates_AndReturnsTheNewETag()
    {
        var (id, etag) = await NewUserAsync();

        var response = await PutAsync(id, "Matching Tag", etag.ToString());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(etag, response.Headers.ETag);
        Assert.Equal(
            response.Headers.ETag,
            (await Admin().GetAsync($"/v1/users/{id}")).Headers.ETag
        );
    }

    [Fact]
    public async Task StaleIfMatch_Is412AndChangesNothing()
    {
        var (id, stale) = await NewUserAsync();
        (await PutAsync(id, "Someone Else", ifMatch: null)).EnsureSuccessStatusCode();

        var response = await PutAsync(id, "Too Late", stale.ToString());

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Someone Else", await NameOfAsync(id));
    }

    [Fact]
    public async Task Wildcard_MatchesAnExistingUser()
    {
        var (id, _) = await NewUserAsync();

        Assert.Equal(HttpStatusCode.OK, (await PutAsync(id, "Any Version", "*")).StatusCode);
    }

    [Fact]
    public async Task ListOfTags_MatchesWhenAnyIsCurrent()
    {
        var (id, etag) = await NewUserAsync();

        Assert.Equal(
            HttpStatusCode.PreconditionFailed,
            (await PutAsync(id, "None Match", "\"1\", \"2\"")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.OK,
            (await PutAsync(id, "One Matches", $"\"1\", {etag}, \"2\"")).StatusCode
        );
    }

    [Fact]
    public async Task WeakTag_NeverMatches()
    {
        var (id, etag) = await NewUserAsync();

        var response = await PutAsync(id, "Weak", $"W/{etag.Tag}");

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("\"unterminated")]
    [InlineData("\"1\" garbage")]
    public async Task MalformedIfMatch_Is400(string ifMatch)
    {
        var (id, _) = await NewUserAsync();

        var response = await PutAsync(id, "Malformed", ifMatch);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("*")]
    [InlineData("\"1\"")]
    public async Task MissingUser_Is404(string? ifMatch)
    {
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await PutAsync(int.MaxValue, "Nobody", ifMatch)).StatusCode
        );
    }

    [Fact]
    public async Task TwoWritesWithTheSameIfMatch_ExactlyOneWins()
    {
        var (id, etag) = await NewUserAsync();
        factory.Barrier.Arm(2);

        var responses = await Task.WhenAll(
            PutAsync(id, "Writer A", etag.ToString()),
            PutAsync(id, "Writer B", etag.ToString())
        );

        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.PreconditionFailed],
            responses.Select(r => r.StatusCode).Order()
        );
    }

    [Fact]
    public async Task TwoWritesWithoutIfMatch_OneWinsAndTheOtherIs409()
    {
        var (id, _) = await NewUserAsync();
        factory.Barrier.Arm(2);

        var responses = await Task.WhenAll(
            PutAsync(id, "Writer A", ifMatch: null),
            PutAsync(id, "Writer B", ifMatch: null)
        );

        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.Conflict],
            responses.Select(r => r.StatusCode).Order()
        );
    }

    /// <summary>
    /// When armed, holds each user UPDATE until <c>n</c> of them are waiting, so both writers
    /// have loaded the same version before either saves: a real race, every time.
    /// </summary>
    public sealed class SaveBarrier : SaveChangesInterceptor
    {
        private readonly object _lock = new();
        private int _expected;
        private int _arrived;
        private TaskCompletionSource _released = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        public void Arm(int writers)
        {
            lock (_lock)
            {
                _expected = writers;
                _arrived = 0;
                _released = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
            }
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            var updatesUser = eventData
                .Context!.ChangeTracker.Entries<User>()
                .Any(entry => entry.State == EntityState.Modified);
            Task? wait = null;
            lock (_lock)
            {
                if (updatesUser && _expected > 0)
                {
                    if (++_arrived == _expected)
                    {
                        _expected = 0;
                        _released.TrySetResult();
                    }
                    wait = _released.Task;
                }
            }
            if (wait is not null)
            {
                await wait.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    public sealed class RaceFactory : AppTemplateWebApplicationFactory
    {
        public SaveBarrier Barrier { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
                services.AddSingleton<ISaveChangesInterceptor>(Barrier)
            );
        }
    }
}
