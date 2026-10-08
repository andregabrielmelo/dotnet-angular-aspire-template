using System.Net;
using System.Net.Http.Json;
using AppTemplate.UseCases.Users;
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

    [Fact]
    public async Task UpdateThenGet_ReturnsTheUpdatedUserInsteadOfTheCachedOne()
    {
        var created = await CreateUserAsync("Ada Lovelace", phoneNumber: "5551234");

        // First GET populates the cache
        var before = await _client.GetFromJsonAsync<UserRecord>($"/users/{created.Id}");
        Assert.Equal("Ada Lovelace", before!.Name);

        var updateResponse = await _client.PutAsJsonAsync(
            $"/users/{created.Id}",
            new UpdateUserRequest { Id = created.Id, Name = "Ada King" }
        );
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var after = await _client.GetFromJsonAsync<UserRecord>($"/users/{created.Id}");
        Assert.Equal("Ada King", after!.Name);
        // Round-tripped through the cache's serializer, including the PhoneNumber value object
        Assert.Equal(before.PhoneNumber, after.PhoneNumber);
    }

    [Fact]
    public async Task GetBeforeCreate_DoesNotServeACachedNotFoundAfterCreation()
    {
        // Ids are max+1 and tests in a class run sequentially, so the next id is predictable
        var existing = await CreateUserAsync("Ada Lovelace");
        var nextId = existing.Id + 1;

        // Misses aren't cached, so this 404 can't outlive the user's creation
        var missing = await _client.GetAsync($"/users/{nextId}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var created = await CreateUserAsync("Grace Hopper");
        Assert.Equal(nextId, created.Id);

        var found = await _client.GetAsync($"/users/{nextId}");
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
    }

    private async Task<CreateUserResponse> CreateUserAsync(string name, string? phoneNumber = null)
    {
        var response = await _client.PostAsJsonAsync(
            "/users",
            new CreateUserRequest
            {
                Name = name,
                Email = $"user-{Guid.NewGuid():N}@example.com",
                Password = "Passw0rd!",
                PhoneNumber = phoneNumber,
            }
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateUserResponse>())!;
    }
}
