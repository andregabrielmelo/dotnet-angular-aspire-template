using System.Net;
using System.Net.Http.Json;
using AppTemplate.UseCases;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Users;
using AppTemplate.UseCases.Users.List;
using AppTemplate.Web.Features.UserFeatures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AppTemplate.FunctionalTests.Caching;

/// <summary>
/// Caching must never serve stale or unauthorized data: writes invalidate HybridCache and the
/// output cache, and cached list responses are still gated by the users:read policy.
/// </summary>
public class CachingTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    private static string NewSubject() => $"sub-{Guid.NewGuid():N}";

    private async Task<CurrentUserResponse> ProvisionAsync(string subject) =>
        (
            await factory
                .CreateAuthenticatedClient(subject)
                .GetFromJsonAsync<CurrentUserResponse>("/users/me")
        )!;

    [Fact]
    public async Task GetById_AfterUpdate_ReturnsTheNewName()
    {
        var target = await ProvisionAsync(NewSubject());
        var reader = factory.CreateAuthenticatedClient(NewSubject(), Permission.UsersRead);
        await reader.GetFromJsonAsync<UserRecord>($"/users/{target.Id}"); // now cached

        var update = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersWrite)
            .PutAsJsonAsync($"/users/{target.Id}", new { id = target.Id, name = "Renamed" });
        var afterUpdate = await reader.GetFromJsonAsync<UserRecord>($"/users/{target.Id}");

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Renamed", afterUpdate!.Name);
    }

    [Fact]
    public async Task GetById_AfterDelete_ReturnsNotFound()
    {
        var target = await ProvisionAsync(NewSubject());
        var reader = factory.CreateAuthenticatedClient(NewSubject(), Permission.UsersRead);
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/users/{target.Id}")).StatusCode); // now cached

        await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersDelete)
            .DeleteAsync($"/users/{target.Id}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await reader.GetAsync($"/users/{target.Id}")).StatusCode
        );
    }

    [Fact]
    public async Task Me_AfterBeingRenamed_ReturnsTheNewName()
    {
        var subject = NewSubject();
        var me = await ProvisionAsync(subject); // cached by external id

        await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersWrite)
            .PutAsJsonAsync($"/users/{me.Id}", new { id = me.Id, name = "Renamed By Admin" });
        var afterUpdate = await ProvisionAsync(subject);

        Assert.Equal("Renamed By Admin", afterUpdate.Name);
    }

    [Fact]
    public async Task List_IsOutputCachedForAuthorizedCallersAndEvictedByWrites()
    {
        // ListUsersQueryService uses raw SQL (unsupported by EF InMemory, see ADR 006) - a
        // counting stub also shows when the response came from the output cache.
        var listQuery = new CountingListUsersQueryService();
        var app = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IListUsersQueryService>();
                services.AddSingleton<IListUsersQueryService>(listQuery);
            })
        );
        HttpClient Client(params string[] permissions) =>
            CreateClient(app, NewSubject(), permissions);

        var first = await Client(Permission.UsersRead).GetAsync("/users?page=1&per_page=10");
        var sameFromAnotherReader = await Client(Permission.UsersRead)
            .GetAsync("/users?page=1&per_page=10");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sameFromAnotherReader.StatusCode);
        Assert.Equal(1, listQuery.Calls); // shared response served from the output cache

        // Authorization runs before the output cache: no permission, no cached response.
        var forbidden = await Client().GetAsync("/users?page=1&per_page=10");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // Another page is another cache entry.
        await Client(Permission.UsersRead).GetAsync("/users?page=2&per_page=10");
        Assert.Equal(2, listQuery.Calls);

        // Any user write evicts cached lists.
        var subject = NewSubject();
        var me = await CreateClient(app, subject)
            .GetFromJsonAsync<CurrentUserResponse>("/users/me");
        await CreateClient(app, subject)
            .PutAsJsonAsync($"/users/{me!.Id}", new { id = me.Id, name = "Changed" });
        await Client(Permission.UsersRead).GetAsync("/users?page=1&per_page=10");
        Assert.Equal(3, listQuery.Calls);
    }

    private static HttpClient CreateClient(
        WebApplicationFactory<Program> app,
        string subject,
        params string[] permissions
    )
    {
        var client = app.CreateClient();
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

    private sealed class CountingListUsersQueryService : IListUsersQueryService
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public Task<PagedResult<UserDto>> ListAsync(int page, int perPage)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(new PagedResult<UserDto>([], page, perPage, 0, 0));
        }
    }
}
