using System.Net;
using System.Net.Http.Json;
using AppTemplate.Web.Features.UserFeatures;
using Xunit;

namespace AppTemplate.FunctionalTests.UserFeatures;

public class UserEndpointsTests : IClassFixture<AppTemplateWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UserEndpointsTests(AppTemplateWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateThenGet_ReturnsTheCreatedUser()
    {
        var request = new CreateUserRequest
        {
            Name = "Ada Lovelace",
            Email = $"ada-{Guid.NewGuid():N}@example.com",
            Password = "Passw0rd!",
        };

        var createResponse = await _client.PostAsJsonAsync("/users", request);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateUserResponse>();
        Assert.NotNull(created);
        Assert.Equal("Ada Lovelace", created!.Name);

        var getResponse = await _client.GetAsync($"/users/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task Create_WithInvalidEmail_ReturnsValidationProblem()
    {
        var request = new CreateUserRequest
        {
            Name = "Ada Lovelace",
            Email = "not-an-email",
            Password = "Passw0rd!",
        };

        var response = await _client.PostAsJsonAsync("/users", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithUnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/users/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
