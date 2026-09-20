using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Users.Delete;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.UserFeatures;

public sealed class DeleteUserRequest
{
    public int UserId { get; init; }
}

public class DeleteEndpoint(IMediator _mediator)
    : Endpoint<DeleteUserRequest, Results<NoContent, NotFound, ProblemHttpResult>>
{
    public override void Configure()
    {
        Delete("/users/{UserId}");

        Summary(s =>
        {
            s.Summary = "Delete a user";
            s.Description = "Deletes a user with the specified id.";
            s.ExampleRequest = new DeleteUserRequest { UserId = 1 };

            s.Responses[400] = "Invalid request data";
        });

        Tags("Users");

        Description(builder =>
            builder
                .Accepts<DeleteUserRequest>()
                .Produces(statusCode: 204, contentType: "application/json")
                .ProducesProblem(400)
        );
    }

    public override async Task<Results<NoContent, NotFound, ProblemHttpResult>> ExecuteAsync(
        DeleteUserRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new DeleteUserCommand(UserId.From(request.UserId));
        var result = await _mediator.Send(command, cancellationToken);

        return result.ToDeleteResult();
    }
}

public sealed class DeleteUserValidator : Validator<DeleteUserRequest>
{
    public DeleteUserValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Id is required");
    }
}
