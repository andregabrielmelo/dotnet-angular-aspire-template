using System.Net;
using System.Net.Http.Json;
using AppTemplate.UseCases.Authorization;
using AppTemplate.Web.Features.UserFeatures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AppTemplate.FunctionalTests.Caching;

/// <summary>
/// The cache is optional: with the distributed cache (Redis in production) failing on every
/// call, reads and writes still succeed. A write that already reached the database must never
/// turn into a 500 because invalidation couldn't reach the cache.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class CacheOutageTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    private static string NewSubject() => $"sub-{Guid.NewGuid():N}";

    private WebApplicationFactory<Program> WithBrokenDistributedCache() =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDistributedCache>();
                services.AddSingleton<IDistributedCache, AlwaysFailingDistributedCache>();
            })
        );

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

    [Fact]
    public async Task ProvisionReadAndUpdate_WhileTheDistributedCacheIsDown_Succeed()
    {
        var app = WithBrokenDistributedCache();

        var me = await CreateClient(app, NewSubject()).GetAsync("/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var user = await me.Content.ReadFromJsonAsync<CurrentUserResponse>();

        var read = await CreateClient(app, NewSubject(), Permission.UsersRead)
            .GetAsync($"/v1/users/{user!.Id}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var update = await CreateClient(app, NewSubject(), Permission.UsersWrite)
            .PutAsJsonAsync($"/v1/users/{user.Id}", new { id = user.Id, name = "Renamed" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
    }

    [Fact]
    public async Task Delete_WhileTheDistributedCacheIsDown_Succeeds()
    {
        var app = WithBrokenDistributedCache();
        var user = await CreateClient(app, NewSubject())
            .GetFromJsonAsync<CurrentUserResponse>("/v1/users/me");

        var delete = await CreateClient(app, NewSubject(), Permission.UsersDelete)
            .DeleteAsync($"/v1/users/{user!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    /// <summary>Stands in for an unreachable Redis.</summary>
    private sealed class AlwaysFailingDistributedCache : IDistributedCache
    {
        private static Exception Outage() => new InvalidOperationException("Cache is down");

        public byte[]? Get(string key) => throw Outage();

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            throw Outage();

        public void Refresh(string key) => throw Outage();

        public Task RefreshAsync(string key, CancellationToken token = default) => throw Outage();

        public void Remove(string key) => throw Outage();

        public Task RemoveAsync(string key, CancellationToken token = default) => throw Outage();

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            throw Outage();

        public Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
            CancellationToken token = default
        ) => throw Outage();
    }
}
