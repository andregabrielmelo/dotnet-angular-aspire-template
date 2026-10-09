using System.Net;
using AppTemplate.UseCases.Authorization;
using Xunit;

namespace AppTemplate.FunctionalTests.Security;

/// <summary>
/// The API's minimal header set: <c>nosniff</c> on every response, and <c>no-store</c> on
/// responses for a signed-in caller, including errors and output-cached responses.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class ApiSecurityHeadersTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    private static string NewSubject() => $"sub-{Guid.NewGuid():N}";

    [Fact]
    public async Task AuthenticatedResponse_IsNosniffAndNoStore()
    {
        var response = await factory
            .CreateAuthenticatedClient(NewSubject())
            .GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNosniff(response);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task AuthenticatedErrorResponse_IsNosniffAndNoStore()
    {
        var response = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersRead)
            .GetAsync("/v1/users/0"); // fails validation

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertNosniff(response);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task OutputCachedResponse_IsStillNoStoreForTheBrowser()
    {
        var client = factory.CreateAuthenticatedClient(NewSubject(), Permission.UsersRead);

        await client.GetAsync("/v1/users"); // stores it in the output cache
        var cached = await client.GetAsync("/v1/users");

        Assert.Equal(HttpStatusCode.OK, cached.StatusCode);
        Assert.True(
            cached.Headers.Age is not null,
            "expected the second response from the output cache"
        );
        AssertNosniff(cached);
        Assert.True(cached.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task AnonymousResponse_IsNosniffWithoutCacheControl()
    {
        var response = await factory.CreateClient().GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertNosniff(response);
        Assert.Null(response.Headers.CacheControl);
    }

    private static void AssertNosniff(HttpResponseMessage response) =>
        Assert.Equal(["nosniff"], response.Headers.GetValues("X-Content-Type-Options"));
}
