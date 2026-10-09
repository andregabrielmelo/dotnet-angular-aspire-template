using System.Net;
using System.Net.Http.Json;
using AppTemplate.UseCases.Users;
using AppTemplate.Web.Features.UserFeatures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppTemplate.FunctionalTests.UserFeatures;

/// <summary>
/// Simulates Redis being down: every distributed (L2) cache call throws. Reads must fall back to
/// the database and writes must not fail just because invalidation couldn't reach Redis.
/// </summary>
public class CacheOutageTests : IClassFixture<CacheOutageTests.FailingCacheWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CacheOutageTests(FailingCacheWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UpdateThenGet_SucceedsWhileTheDistributedCacheIsDown()
    {
        var created = await CreateUserAsync("Ada Lovelace");

        var before = await _client.GetFromJsonAsync<UserRecord>($"/users/{created.Id}");
        Assert.Equal("Ada Lovelace", before!.Name);

        var updateResponse = await _client.PutAsJsonAsync(
            $"/users/{created.Id}",
            new UpdateUserRequest { Id = created.Id, Name = "Ada King" }
        );
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var after = await _client.GetFromJsonAsync<UserRecord>($"/users/{created.Id}");
        Assert.Equal("Ada King", after!.Name);
    }

    [Fact]
    public async Task DeleteThenGet_SucceedsWhileTheDistributedCacheIsDown()
    {
        var created = await CreateUserAsync("Grace Hopper");
        var found = await _client.GetAsync($"/users/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);

        var deleteResponse = await _client.DeleteAsync($"/users/{created.Id}");
        Assert.True(deleteResponse.IsSuccessStatusCode);

        var missing = await _client.GetAsync($"/users/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private async Task<CreateUserResponse> CreateUserAsync(string name)
    {
        var response = await _client.PostAsJsonAsync(
            "/users",
            new CreateUserRequest
            {
                Name = name,
                Email = $"user-{Guid.NewGuid():N}@example.com",
                Password = "Passw0rd!",
            }
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateUserResponse>())!;
    }

    public sealed class FailingCacheWebApplicationFactory : AppTemplateWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            // HybridCache uses the registered IDistributedCache as its L2
            builder.ConfigureServices(services =>
                services.AddSingleton<IDistributedCache, ThrowingDistributedCache>()
            );
        }
    }

    private sealed class ThrowingDistributedCache : IDistributedCache
    {
        private static InvalidOperationException Down() => new("Distributed cache is down");

        public byte[]? Get(string key) => throw Down();

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            Task.FromException<byte[]?>(Down());

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            throw Down();

        public Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
            CancellationToken token = default
        ) => Task.FromException(Down());

        public void Refresh(string key) => throw Down();

        public Task RefreshAsync(string key, CancellationToken token = default) =>
            Task.FromException(Down());

        public void Remove(string key) => throw Down();

        public Task RemoveAsync(string key, CancellationToken token = default) =>
            Task.FromException(Down());
    }
}
