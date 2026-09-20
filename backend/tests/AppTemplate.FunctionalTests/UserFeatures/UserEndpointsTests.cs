using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AppTemplate.FunctionalTests.AuthFeatures;
using AppTemplate.Web.Features.AuthFeatures;
using Xunit;

namespace AppTemplate.FunctionalTests.UserFeatures;

public class UserEndpointsTests : IClassFixture<AppTemplateWebApplicationFactory>
{
    private readonly AppTemplateWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UserEndpointsTests(AppTemplateWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetById_WithoutBearerToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/users/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_WithoutBearerToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithBearerTokenAndUnknownId_ReturnsNotFound()
    {
        var accessToken = await AuthTestHelper.RegisterConfirmAndLoginAsync(_factory, _client);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/users/999999");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
