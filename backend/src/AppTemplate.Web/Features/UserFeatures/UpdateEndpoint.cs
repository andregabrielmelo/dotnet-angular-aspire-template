using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Users;
using AppTemplate.UseCases.Users.Update;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.UserFeatures;

public sealed class UpdateUserRequest
{
    public int Id { get; set; }

    public required string Name { get; set; }
    public string? PhoneNumber { get; set; }
}

public sealed record UpdateUserResponse(UserRecord User);

public class UpdateEndpoint(IMediator _mediator)
    : Endpoint<
        UpdateUserRequest,
        Results<Ok<UpdateUserResponse>, NotFound, ProblemHttpResult>,
        UpdateUserMapper
    >
{
    public override void Configure()
    {
        Put("/users/{id}");

        Summary(s =>
        {
            s.Summary = "Update a user";
            s.Description =
                "Updates a user's name and phone number. Users may update their own profile; "
                + "updating anyone else's requires the users:write permission.";
            s.ExampleRequest = new UpdateUserRequest { Id = 1, Name = "Sample User" };
            s.ResponseExamples[200] = new UpdateUserResponse(
                new UserRecord(1, "Sample User", null)
            );

            s.Responses[200] = "User updated successfully";
            s.Responses[400] = "Invalid request data";
            s.Responses[403] = "Not your profile, and no users:write permission";
            s.Responses[404] = "User not found";
        });

        Tags("Users");

        Description(builder =>
            builder
                .Accepts<UpdateUserRequest>()
                .Produces<UpdateUserResponse>(200, "application/json")
                .ProducesProblem(400)
                .ProducesProblem(403)
                .ProducesProblem(404)
        );
    }

    public override async Task<
        Results<Ok<UpdateUserResponse>, NotFound, ProblemHttpResult>
    > ExecuteAsync(UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateUserCommand(
            UserId.From(request.Id),
            UserName.From(request.Name),
            request.PhoneNumber
        );
        var result = await _mediator.Send(command, cancellationToken);

        return result.ToUpdateResult<UserDto, UpdateUserResponse>(Map.FromEntity);
    }
}

public sealed class UpdateUserValidator : Validator<UpdateUserRequest>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MinimumLength(2)
            .MaximumLength(UserName.MaxLength)
            .WithMessage($"User name must not exceed {UserName.MaxLength} characters");
        RuleFor(x => x.Id)
            .Must((args, userId) => args.Id == userId)
            .WithMessage(
                "Route and body Ids must match; cannot update Id of an existing resource."
            );
    }
}

public sealed class UpdateUserMapper : Mapper<UpdateUserRequest, UpdateUserResponse, UserDto>
{
    public override UpdateUserResponse FromEntity(UserDto e) =>
        new(new UserRecord(e.Id.Value, e.Name.Value, ""));
}
