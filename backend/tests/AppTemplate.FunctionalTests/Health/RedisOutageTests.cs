using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Users;
using AppTemplate.Web.Features.UserFeatures;
using Microsoft.AspNetCore.Hosting;
using Testcontainers.Redis;
using Xunit;

namespace AppTemplate.FunctionalTests.Health;

/// <summary>
/// Redis is optional: with it stopped, the API keeps answering (HybridCache falls back to its
/// in-memory layer and Postgres, the output cache and invalidation fail open), readiness stays
/// healthy, and only /health/dependencies reports Degraded. Without Redis every cache call
/// skips it at once (FailOpenRedis) instead of waiting out the client's 5-second timeout.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class RedisOutageTests(RedisOutageTests.RedisFactory factory)
    : IClassFixture<RedisOutageTests.RedisFactory>
{
    [Fact]
    public async Task WithRedisStopped_TheApiStillServesAndOnlyDependenciesDegrade()
    {
        var subject = $"sub-{Guid.NewGuid():N}";
        var me = await factory
            .CreateAuthenticatedClient(subject)
            .GetFromJsonAsync<CurrentUserResponse>("/v1/users/me");
        var reader = factory.CreateAuthenticatedClient(
            $"sub-{Guid.NewGuid():N}",
            Permission.UsersRead
        );
        var writer = factory.CreateAuthenticatedClient(
            $"sub-{Guid.NewGuid():N}",
            Permission.UsersWrite
        );
        var anonymous = factory.CreateClient();

        // Warm both cache layers and confirm Redis is reported healthy while it runs.
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/v1/users/{me!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/v1/users")).StatusCode);
        Assert.Equal("Healthy", await anonymous.GetStringAsync("/health/dependencies"));

        await factory.Redis.StopAsync();
        var stopwatch = Stopwatch.StartNew();

        var byId = await reader.GetAsync($"/v1/users/{me.Id}");
        var list = await reader.GetAsync("/v1/users");
        var update = await writer.PutAsJsonAsync(
            $"/v1/users/{me.Id}",
            new { id = me.Id, name = "Renamed Without Redis" }
        );
        var afterUpdate = await reader.GetFromJsonAsync<UserRecord>($"/v1/users/{me.Id}");
        var health = await anonymous.GetAsync("/health");
        var dependencies = await anonymous.GetAsync("/health/dependencies");

        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Renamed Without Redis", afterUpdate!.Name);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal("Healthy", await health.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, dependencies.StatusCode);
        Assert.Equal("Degraded", await dependencies.Content.ReadAsStringAsync());
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"requests without Redis took {stopwatch.Elapsed}"
        );
    }

    public sealed class RedisFactory : AppTemplateWebApplicationFactory
    {
        public RedisContainer Redis { get; } = new RedisBuilder("redis:8.2").Build();

        public override async Task InitializeAsync()
        {
            await Redis.StartAsync();
            await base.InitializeAsync();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("ConnectionStrings:cache", Redis.GetConnectionString());
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await Redis.DisposeAsync();
        }
    }
}
