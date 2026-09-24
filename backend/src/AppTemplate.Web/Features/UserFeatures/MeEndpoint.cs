using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Users.GetOrCreateCurrent;

namespace AppTemplate.Web.Features.UserFeatures;

public sealed record CurrentUserResponse(int Id, string Name, string Email);

/// <summary>
/// Returns the caller's own profile, creating it on first use (just-in-time provisioning) -
/// users register in Keycloak, so this is where the domain User row comes into existence.
/// </summary>
public class MeEndpoint(IMediator _mediator)
    : EndpointWithoutRequest<Results<Ok<CurrentUserResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("/users/me");

        Summary(s =>
        {
            s.Summary = "Get the current user";
            s.Description =
                "Returns the authenticated caller's profile, creating it from the access token's claims on first call.";
            s.ResponseExamples[200] = new CurrentUserResponse(
                1,
                "Sample User",
                "sample@example.com"
            );

            s.Responses[200] = "Current user returned successfully";
            s.Responses[400] = "The access token is missing a required claim";
            s.Responses[401] = "Missing or invalid access token";
            s.Responses[409] = "The token's email is already linked to another user";
        });

        Tags("Users");

        Description(builder =>
            builder
                .Produces<CurrentUserResponse>(200, "application/json")
                .ProducesProblem(400)
                .ProducesProblem(401)
                .ProducesProblem(409)
        );
    }

    public override async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> ExecuteAsync(
        CancellationToken cancellationToken
    )
    {
        var externalId = User.FindFirst("sub")?.Value;
        var email = User.FindFirst("email")?.Value;
        var name = FirstValidUserName(
            User.FindFirst("name")?.Value,
            User.FindFirst("preferred_username")?.Value,
            email
        );

        if (
            string.IsNullOrWhiteSpace(externalId)
            || string.IsNullOrWhiteSpace(email)
            || name is null
        )
        {
            return TypedResults.Problem(
                title: "Incomplete identity",
                detail: "The access token must carry 'sub', 'email' and a usable name claim.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var command = new GetOrCreateCurrentUserCommand(
            externalId,
            name.Value,
            new EmailAddress(email)
        );
        var result = await _mediator.Send(command, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Ok => TypedResults.Ok(
                new CurrentUserResponse(
                    result.Value.Id.Value,
                    result.Value.Name.Value,
                    result.Value.Email.Value
                )
            ),
            ResultStatus.Conflict => TypedResults.Problem(
                title: "Conflict",
                detail: string.Join("; ", result.Errors),
                statusCode: StatusCodes.Status409Conflict
            ),
            _ => TypedResults.Problem(
                title: "Request failed",
                detail: string.Join("; ", result.Errors),
                statusCode: StatusCodes.Status400BadRequest
            ),
        };
    }

    private static UserName? FirstValidUserName(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate is not null && UserName.TryFrom(candidate, out var userName))
            {
                return userName;
            }
        }

        return null;
    }
}
