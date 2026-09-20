using System.Security.Claims;

namespace AppTemplate.Web.Features.AuthFeatures;

public sealed record MeResponse(int Id, string Name, string Email);

public class MeEndpoint : EndpointWithoutRequest<Results<Ok<MeResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("/auth/me");
        // No AllowAnonymous() - FastEndpoints requires authentication by default, so a
        // missing/invalid bearer token short-circuits to 401 before ExecuteAsync ever runs.

        Summary(s =>
        {
            s.Summary = "Get the current authenticated user";
            s.Description =
                "Built straight from the access token's claims - no DB round trip, so a profile change won't show here until the token refreshes.";
            s.Responses[200] = "The current user";
            s.Responses[401] = "Missing or invalid access token";
        });

        Tags("Auth");

        Description(builder =>
            builder.Produces<MeResponse>(200, "application/json").ProducesProblem(401)
        );
    }

    public override Task<Results<Ok<MeResponse>, ProblemHttpResult>> ExecuteAsync(
        CancellationToken cancellationToken
    )
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var name = User.FindFirstValue(ClaimTypes.Name)!;
        var email = User.FindFirstValue(ClaimTypes.Email)!;

        return Task.FromResult<Results<Ok<MeResponse>, ProblemHttpResult>>(
            TypedResults.Ok(new MeResponse(id, name, email))
        );
    }
}
