using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AppTemplate.Web.Features.AuthFeatures;
using AppTemplate.Web.Features.UserFeatures;
using Xunit;

namespace AppTemplate.FunctionalTests.AuthFeatures;

public class AuthEndpointsTests : IClassFixture<AppTemplateWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(AppTemplateWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static RegisterRequest NewRegisterRequest() =>
        new()
        {
            Name = "Ada Lovelace",
            Email = $"ada-{Guid.NewGuid():N}@example.com",
            Password = "Passw0rd!",
        };

    [Fact]
    public async Task Register_WithNewEmail_ReturnsTokens()
    {
        var response = await _client.PostAsJsonAsync("/register", NewRegisterRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = await response.Content.ReadFromJsonAsync<AuthTokensResponse>();
        Assert.NotNull(tokens);
        Assert.False(string.IsNullOrEmpty(tokens!.AccessToken));
        Assert.False(string.IsNullOrEmpty(tokens.RefreshToken));
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsValidationProblem()
    {
        var request = NewRegisterRequest();
        await _client.PostAsJsonAsync("/register", request);

        var response = await _client.PostAsJsonAsync("/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsTokens()
    {
        var registerRequest = NewRegisterRequest();
        await _client.PostAsJsonAsync("/register", registerRequest);

        var response = await _client.PostAsJsonAsync(
            "/login",
            new LoginRequest { Email = registerRequest.Email, Password = registerRequest.Password }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var registerRequest = NewRegisterRequest();
        await _client.PostAsJsonAsync("/register", registerRequest);

        var response = await _client.PostAsJsonAsync(
            "/login",
            new LoginRequest { Email = registerRequest.Email, Password = "WrongPassword1!" }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewTokens()
    {
        var registerResponse = await _client.PostAsJsonAsync("/register", NewRegisterRequest());
        var tokens = await registerResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();

        var response = await _client.PostAsJsonAsync(
            "/refresh",
            new RefreshRequest { RefreshToken = tokens!.RefreshToken }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var newTokens = await response.Content.ReadFromJsonAsync<AuthTokensResponse>();
        Assert.NotEqual(tokens.AccessToken, newTokens!.AccessToken);
        Assert.NotEqual(tokens.RefreshToken, newTokens.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            "/refresh",
            new RefreshRequest { RefreshToken = "not-a-real-token" }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_ThenRefreshWithSameToken_ReturnsUnauthorized()
    {
        var registerResponse = await _client.PostAsJsonAsync("/register", NewRegisterRequest());
        var tokens = await registerResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();

        var logoutResponse = await _client.PostAsJsonAsync(
            "/logout",
            new LogoutRequest { RefreshToken = tokens!.RefreshToken }
        );
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refreshResponse = await _client.PostAsJsonAsync(
            "/refresh",
            new RefreshRequest { RefreshToken = tokens.RefreshToken }
        );
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_WithUnknownToken_IsIdempotentAndSucceeds()
    {
        var response = await _client.PostAsJsonAsync(
            "/logout",
            new LogoutRequest { RefreshToken = "not-a-real-token" }
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutBearerToken_ReturnsUnauthorized()
    {
        var response = await _client.PutAsJsonAsync(
            "/users/1",
            new UpdateUserRequest { Id = 1, Name = "Someone Else" }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithBearerToken_Succeeds()
    {
        var registerResponse = await _client.PostAsJsonAsync("/register", NewRegisterRequest());
        var tokens = await registerResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/users/{tokens!.UserId}")
        {
            Content = JsonContent.Create(
                new UpdateUserRequest { Id = tokens.UserId, Name = "Ada L." }
            ),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
