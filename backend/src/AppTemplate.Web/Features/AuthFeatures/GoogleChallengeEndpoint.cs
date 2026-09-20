using Microsoft.AspNetCore.Authentication.Google;

namespace AppTemplate.Web.Features.AuthFeatures;

/// <summary>
/// Not a JSON API call - navigate the browser here directly (e.g. a plain anchor tag), since
/// this redirects to Google's own consent screen rather than returning a response body.
/// </summary>
public class GoogleChallengeEndpoint : EndpointWithoutRequest<ChallengeHttpResult>
{
    public override void Configure()
    {
        Get("/auth/external/google");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Sign in with Google";
            s.Description = "Redirects to Google's consent screen.";
            s.Responses[302] = "Redirect to Google";
        });

        Tags("Auth");
    }

    public override Task<ChallengeHttpResult> ExecuteAsync(CancellationToken cancellationToken) =>
        Task.FromResult(
            TypedResults.Challenge(authenticationSchemes: [GoogleDefaults.AuthenticationScheme])
        );
}
