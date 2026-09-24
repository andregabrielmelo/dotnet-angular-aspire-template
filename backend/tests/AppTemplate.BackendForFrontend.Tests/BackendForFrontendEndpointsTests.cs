using System.Net;
using System.Net.Http.Json;
using AppTemplate.BackendForFrontend.Configurations;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace AppTemplate.BackendForFrontend.Tests;

public class BackendForFrontendEndpointsTests(BackendForFrontendFactory factory)
    : IClassFixture<BackendForFrontendFactory>
{
    private readonly HttpClient _client = factory.CreateBrowserClient();

    private static Dictionary<string, string> RedirectQuery(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.StartsWith(BackendForFrontendFactory.AuthorizationEndpoint, location.ToString());
        return QueryHelpers
            .ParseQuery(location.Query)
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToString());
    }

    [Fact]
    public async Task Providers_ListsOnlyEnabledProviders()
    {
        var providers = await _client.GetFromJsonAsync<ExternalIdentityProvider[]>(
            "/backend-for-frontend/providers"
        );

        var provider = Assert.Single(providers!);
        Assert.Equal(new ExternalIdentityProvider("google", "Google"), provider);
    }

    [Fact]
    public async Task Login_StartsTheCodeFlowWithPkce()
    {
        var query = RedirectQuery(await _client.GetAsync("/backend-for-frontend/login"));

        Assert.Equal("code", query["response_type"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal("apptemplate-backend-for-frontend", query["client_id"]);
        Assert.DoesNotContain("kc_idp_hint", query.Keys);
    }

    [Fact]
    public async Task Login_WithEnabledProvider_SendsKeycloakTheProviderHint()
    {
        var query = RedirectQuery(
            await _client.GetAsync("/backend-for-frontend/login?provider=google")
        );

        Assert.Equal("google", query["kc_idp_hint"]);
    }

    [Theory]
    [InlineData("github")]
    [InlineData("unknown")]
    public async Task Login_WithDisabledOrUnknownProvider_IsRejected(string provider)
    {
        var response = await _client.GetAsync($"/backend-for-frontend/login?provider={provider}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_AsksKeycloakForTheRegistrationForm()
    {
        var query = RedirectQuery(await _client.GetAsync("/backend-for-frontend/register"));

        Assert.Equal("create", query["prompt"]);
    }

    [Fact]
    public async Task User_WithoutSession_ReturnsUnauthorizedInsteadOfRedirecting()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/backend-for-frontend/user");
        request.Headers.Add(AntiforgeryHeaderMiddleware.HeaderName, "1");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/users/me")]
    [InlineData("/backend-for-frontend/user")]
    public async Task CallsWithoutCsrfHeader_AreRejected(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Providers_RepeatRequests_AreServedFromTheOutputCache()
    {
        await _client.GetAsync("/backend-for-frontend/providers");

        var cached = await _client.GetAsync("/backend-for-frontend/providers");

        // The output cache adds Age to responses it serves from its store.
        Assert.Equal(HttpStatusCode.OK, cached.StatusCode);
        Assert.NotNull(cached.Headers.Age);
    }
}
