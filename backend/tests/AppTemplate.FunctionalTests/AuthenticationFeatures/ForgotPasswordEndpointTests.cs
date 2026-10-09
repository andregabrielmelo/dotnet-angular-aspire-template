using System.Net;
using System.Net.Http.Json;
using AppTemplate.Web.Features.AuthenticationFeatures;
using Ardalis.Result;
using Xunit;

namespace AppTemplate.FunctionalTests.AuthenticationFeatures;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
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
            .PostAsJsonAsync("/v1/password-reset", new ForgotPasswordRequest { Email = email });

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
            .PostAsJsonAsync("/v1/password-reset", new ForgotPasswordRequest { Email = email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithInvalidEmail_ReturnsBadRequest()
    {
        var response = await CreateAnonymousClient()
            .PostAsJsonAsync(
                "/v1/password-reset",
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
            var allowed = await client.PostAsJsonAsync("/v1/password-reset", request);
            Assert.Equal(HttpStatusCode.Accepted, allowed.StatusCode);
        }

        var throttled = await client.PostAsJsonAsync("/v1/password-reset", request);

        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }

    [Fact]
    public async Task Throttle_CannotBeBypassedWithASpoofedForwardedFor()
    {
        // A client connecting directly (not through the trusted proxy) rotates X-Forwarded-For
        // on every call. The throttle must still count all of them against its real address.
        var n = 0;
        Task<HttpResponseMessage> SendWithNewSpoofedAddress()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/v1/password-reset")
            {
                Content = JsonContent.Create(
                    new ForgotPasswordRequest { Email = "ada@example.com" }
                ),
            };
            request.Headers.Add(TestPeerAddress.HeaderName, "203.0.113.8");
            request.Headers.Add("X-Forwarded-For", $"198.51.100.{++n}");
            return factory.CreateClient().SendAsync(request);
        }

        for (var i = 0; i < ForgotPasswordEndpoint.RequestsPerWindow; i++)
        {
            Assert.Equal(HttpStatusCode.Accepted, (await SendWithNewSpoofedAddress()).StatusCode);
        }
        var throttled = await SendWithNewSpoofedAddress();

        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.Equal("application/problem+json", throttled.Content.Headers.ContentType?.MediaType);
        var body = await throttled.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(429, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
    }
}
