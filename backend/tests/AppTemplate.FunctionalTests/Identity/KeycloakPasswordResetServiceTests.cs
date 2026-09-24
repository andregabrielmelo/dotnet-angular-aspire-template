using System.Net;
using System.Text;
using AppTemplate.Core.ValueObjects;
using AppTemplate.Infrastructure.Identity;
using Ardalis.Result;
using Duende.AccessTokenManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AppTemplate.FunctionalTests.Identity;

/// <summary>
/// Exercises the Keycloak Admin API client against a stubbed HTTP handler: which requests it
/// makes, and how it maps Keycloak's answers to results.
/// </summary>
public class KeycloakPasswordResetServiceTests
{
    private static readonly KeycloakAdminOptions Options = new()
    {
        ClientSecret = "secret",
        PasswordResetLinkLifespan = TimeSpan.FromMinutes(15),
    };

    private readonly List<HttpRequestMessage> _requests = [];
    private readonly List<string?> _bodies = [];

    private KeycloakPasswordResetService CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> respond
    )
    {
        var handler = new StubHandler(async request =>
        {
            _requests.Add(request);
            _bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync());
            return respond(request);
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://keycloak.test/") };

        return new KeycloakPasswordResetService(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(Options),
            NullLogger<KeycloakPasswordResetService>.Instance
        );
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    [Fact]
    public async Task SendPasswordResetEmail_ForExistingUser_ExecutesUpdatePasswordAction()
    {
        var service = CreateService(request =>
            request.Method == HttpMethod.Get
                ? Json("""[{ "id": "user-123", "username": "ada" }]""")
                : new HttpResponseMessage(HttpStatusCode.NoContent)
        );

        var result = await service.SendPasswordResetEmailAsync(
            new EmailAddress("ada+test@example.com"),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _requests.Count);
        Assert.Equal(
            "/admin/realms/apptemplate/users?email=ada%2Btest%40example.com&exact=true&briefRepresentation=true",
            _requests[0].RequestUri!.PathAndQuery
        );

        var put = _requests[1];
        Assert.Equal(HttpMethod.Put, put.Method);
        Assert.Equal(
            "/admin/realms/apptemplate/users/user-123/execute-actions-email",
            put.RequestUri!.AbsolutePath
        );
        Assert.Contains("client_id=apptemplate-backend-for-frontend", put.RequestUri.Query);
        Assert.Contains("redirect_uri=https%3A%2F%2Flocalhost%3A7100%2Fauth", put.RequestUri.Query);
        Assert.Contains("lifespan=900", put.RequestUri.Query);
        Assert.Equal("""["UPDATE_PASSWORD"]""", _bodies[1]);
    }

    [Fact]
    public async Task SendPasswordResetEmail_ForUnknownEmail_ReturnsNotFoundWithoutSending()
    {
        var service = CreateService(_ => Json("[]"));

        var result = await service.SendPasswordResetEmailAsync(
            new EmailAddress("nobody@example.com"),
            CancellationToken.None
        );

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Single(_requests);
    }

    [Fact]
    public async Task SendPasswordResetEmail_WhenKeycloakRejectsTheAction_ReturnsError()
    {
        var service = CreateService(request =>
            request.Method == HttpMethod.Get
                ? Json("""[{ "id": "user-123" }]""")
                : new HttpResponseMessage(HttpStatusCode.InternalServerError)
        );

        var result = await service.SendPasswordResetEmailAsync(
            new EmailAddress("ada@example.com"),
            CancellationToken.None
        );

        Assert.Equal(ResultStatus.Error, result.Status);
    }

    [Fact]
    public async Task SendPasswordResetEmail_WhenKeycloakIsUnreachable_ReturnsUnavailable()
    {
        var service = CreateService(_ => throw new HttpRequestException("connection refused"));

        var result = await service.SendPasswordResetEmailAsync(
            new EmailAddress("ada@example.com"),
            CancellationToken.None
        );

        Assert.Equal(ResultStatus.Unavailable, result.Status);
    }

    [Fact]
    public void AddKeycloakAdministration_ConfiguresTheClientCredentialsClientFromOptions()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?> { ["Keycloak:Admin:ClientSecret"] = "s3cret" }
                )
                .Build()
        );
        services.AddLogging();
        services.AddKeycloakAdministration();

        using var provider = services.BuildServiceProvider();
        var client = provider
            .GetRequiredService<IOptionsMonitor<ClientCredentialsClient>>()
            .Get("keycloak-admin");

        Assert.Equal(
            new Uri("https+http://keycloak/realms/apptemplate/protocol/openid-connect/token"),
            client.TokenEndpoint
        );
        Assert.Equal("apptemplate-user-admin", client.ClientId?.ToString());
        Assert.Equal("s3cret", client.ClientSecret?.ToString());
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => respond(request);
    }
}
