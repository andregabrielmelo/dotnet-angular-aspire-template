using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Users.ProvisionCurrent;

namespace AppTemplate.Web.Features.UserFeatures;

/// <summary>
/// Creates the caller's profile from their access token's claims (just-in-time provisioning)
/// - users register in Keycloak, so this is where the domain User row comes into existence.
/// Idempotent and safe to call concurrently: an existing profile is returned with 200.
/// </summary>
public class ProvisionMeEndpoint(IMediator _mediator, ICurrentUser _currentUser)
    : EndpointWithoutRequest<
        Results<Created<CurrentUserResponse>, Ok<CurrentUserResponse>, ProblemHttpResult>
    >
{
    public override void Configure()
    {
        Post("/users/me");

        Summary(s =>
        {
            s.Summary = "Provision the current user";
            s.Description =
                "Creates the authenticated caller's profile from the access token's claims, or "
                + "returns it unchanged if it already exists.";
            s.ResponseExamples[201] = new CurrentUserResponse(
                1,
                "Sample User",
                "sample@example.com",
                [Permission.UsersRead]
            );

            s.Responses[200] = "Already provisioned - existing profile returned";
            s.Responses[201] = "Profile created";
            s.Responses[400] = "The access token is missing a required claim";
            s.Responses[401] = "Missing or invalid access token";
            s.Responses[409] = "The token's email is already linked to another user";
        });

        Tags("Users");

        Description(builder =>
            builder
                .Produces<CurrentUserResponse>(200, "application/json")
                .Produces<CurrentUserResponse>(201, "application/json")
                .ProducesProblem(400)
                .ProducesProblem(401)
                .ProducesProblem(409)
        );
    }

    public override async Task<
        Results<Created<CurrentUserResponse>, Ok<CurrentUserResponse>, ProblemHttpResult>
    > ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!CurrentUserIdentity.TryRead(User, out var identity))
        {
            return TypedResults.Problem(
                title: CurrentUserIdentity.IncompleteTitle,
                detail: "The access token must carry 'sub', 'email' and a usable name claim.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var command = new ProvisionCurrentUserCommand(
            identity.ExternalId,
            identity.Name,
            identity.Email
        );
        var result = await _mediator.Send(command, cancellationToken);

        if (result.Status == ResultStatus.Ok)
        {
            var response = CurrentUserResponse.From(result.Value.User, _currentUser.Permissions);
            return result.Value.Created
                ? TypedResults.Created("/users/me", response)
                : TypedResults.Ok(response);
        }

        return result.Status == ResultStatus.Conflict
            ? TypedResults.Problem(
                title: "Conflict",
                detail: string.Join("; ", result.Errors),
                statusCode: StatusCodes.Status409Conflict
            )
            : TypedResults.Problem(
                title: "Request failed",
                detail: string.Join("; ", result.Errors),
                statusCode: StatusCodes.Status400BadRequest
            );
    }
}
