using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Users.GetCurrent;

namespace AppTemplate.Web.Features.UserFeatures;

/// <summary>
/// Returns the caller's own profile. Read-only: a caller who hasn't been provisioned yet gets
/// 404 and should <c>POST /users/me</c> (see <see cref="ProvisionMeEndpoint"/>).
/// </summary>
public class MeEndpoint(IMediator _mediator, ICurrentUser _currentUser)
    : EndpointWithoutRequest<Results<Ok<CurrentUserResponse>, NotFound, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("/users/me");

        Summary(s =>
        {
            s.Summary = "Get the current user";
            s.Description =
                "Returns the authenticated caller's profile. 404 until it has been created with POST /users/me.";
            s.ResponseExamples[200] = new CurrentUserResponse(
                1,
                "Sample User",
                "sample@example.com",
                [Permission.UsersRead]
            );

            s.Responses[200] = "Current user returned successfully";
            s.Responses[400] = "The access token has no 'sub' claim";
            s.Responses[401] = "Missing or invalid access token";
            s.Responses[404] = "Not provisioned yet - call POST /users/me";
        });

        Tags("Users");

        Description(builder =>
            builder
                .Produces<CurrentUserResponse>(200, "application/json")
                .ProducesProblem(400)
                .ProducesProblem(401)
                .Produces(404)
        );
    }

    public override async Task<
        Results<Ok<CurrentUserResponse>, NotFound, ProblemHttpResult>
    > ExecuteAsync(CancellationToken cancellationToken)
    {
        var externalId = CurrentUserIdentity.ReadExternalId(User);
        if (externalId is null)
        {
            return TypedResults.Problem(
                title: CurrentUserIdentity.IncompleteTitle,
                detail: "The access token must carry a 'sub' claim.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var result = await _mediator.Send(new GetCurrentUserQuery(externalId), cancellationToken);

        return result.Status switch
        {
            ResultStatus.Ok => TypedResults.Ok(
                CurrentUserResponse.From(result.Value, _currentUser.Permissions)
            ),
            ResultStatus.NotFound => TypedResults.NotFound(),
            _ => TypedResults.Problem(
                title: "Request failed",
                detail: string.Join("; ", result.Errors),
                statusCode: StatusCodes.Status400BadRequest
            ),
        };
    }
}
