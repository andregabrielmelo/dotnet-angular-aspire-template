using System.Net;
using System.Net.Http.Json;
using AppTemplate.Web.Features.AuthenticationFeatures;
using Ardalis.Result;
using Xunit;

namespace AppTemplate.FunctionalTests.AuthenticationFeatures;

public class ForgotPasswordEndpointTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    private static int _nextClientAddress;

    // Throttling is per client IP, so every client gets its own address. A counter (not a
    // random number) guarantees two tests never share a throttling window.
    private HttpClient CreateAnonymousClient()
    {
        var n = Interlocked.Increment(ref _nextClientAddress);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.0.{n / 250}.{n % 250 + 1}");
        return client;
    }

    [Fact]
    public async Task Post_WithoutAuthentication_IsAcceptedAndStartsTheReset()
    {
        var email = $"ada-{Guid.NewGuid():N}@example.com";

        var response = await CreateAnonymousClient()
            .PostAsJsonAsync("/password-reset", new ForgotPasswordRequest { Email = email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Contains(email, factory.PasswordResetService.RequestedEmails);
    }

    [Fact]
    public async Task Post_WithUnknownEmail_LooksExactlyLikeSuccess()
    {
        var email = $"nobody-{Guid.NewGuid():N}@example.com";
        factory.PasswordResetService.Respond = e =>
            e.Value == email ? Result.NotFound() : Result.Success();

        var response = await CreateAnonymousClient()
            .PostAsJsonAsync("/password-reset", new ForgotPasswordRequest { Email = email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithInvalidEmail_ReturnsBadRequest()
    {
        var response = await CreateAnonymousClient()
            .PostAsJsonAsync(
                "/password-reset",
                new ForgotPasswordRequest { Email = "not-an-email" }
            );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_TooManyTimesFromOneClient_IsThrottled()
    {
        var client = CreateAnonymousClient();
        var request = new ForgotPasswordRequest { Email = "ada@example.com" };

        for (var i = 0; i < ForgotPasswordEndpoint.RequestsPerWindow; i++)
        {
            var allowed = await client.PostAsJsonAsync("/password-reset", request);
            Assert.Equal(HttpStatusCode.Accepted, allowed.StatusCode);
        }

        var throttled = await client.PostAsJsonAsync("/password-reset", request);

        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }
}
