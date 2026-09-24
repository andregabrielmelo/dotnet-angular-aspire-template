using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AppTemplate.FunctionalTests;

/// <summary>
/// Stands in for Keycloak-issued JWTs in functional tests: a request carrying
/// <see cref="UserHeader"/> is authenticated as that <c>sub</c>, with the same claim names a
/// real access token has. Requests without it stay anonymous.
/// </summary>
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (
            !Request.Headers.TryGetValue(UserHeader, out var subject)
            || string.IsNullOrEmpty(subject)
        )
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim("sub", subject!),
            new Claim("name", $"Test {subject}"),
            new Claim("email", $"{subject}@example.com"),
        };
        var identity = new ClaimsIdentity(claims, SchemeName, "name", "roles");

        return Task.FromResult(
            AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)
            )
        );
    }
}
