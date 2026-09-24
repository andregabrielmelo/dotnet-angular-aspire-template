using System.Net;
using System.Net.Http.Json;
using AppTemplate.Web.Features.UserFeatures;
using Xunit;

namespace AppTemplate.FunctionalTests.UserFeatures;

public class UserEndpointsTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    [Fact]
    public async Task List_WithoutAuthentication_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_OnFirstCall_ProvisionsTheUserFromTokenClaims()
    {
        var subject = $"sub-{Guid.NewGuid():N}";
        var client = factory.CreateAuthenticatedClient(subject);

        var response = await client.GetAsync("/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(me);
        Assert.Equal($"Test {subject}", me!.Name);
        Assert.Equal($"{subject}@example.com", me.Email);
    }

    [Fact]
    public async Task Me_CalledTwice_ReturnsTheSameUser()
    {
        var client = factory.CreateAuthenticatedClient($"sub-{Guid.NewGuid():N}");

        var first = await client.GetFromJsonAsync<CurrentUserResponse>("/users/me");
        var second = await client.GetFromJsonAsync<CurrentUserResponse>("/users/me");

        Assert.Equal(first!.Id, second!.Id);
    }

    [Fact]
    public async Task MeThenGetById_ReturnsTheProvisionedUser()
    {
        var client = factory.CreateAuthenticatedClient($"sub-{Guid.NewGuid():N}");
        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/users/me");

        var response = await client.GetAsync($"/users/{me!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithUnknownId_ReturnsNotFound()
    {
        var client = factory.CreateAuthenticatedClient($"sub-{Guid.NewGuid():N}");

        var response = await client.GetAsync("/users/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
