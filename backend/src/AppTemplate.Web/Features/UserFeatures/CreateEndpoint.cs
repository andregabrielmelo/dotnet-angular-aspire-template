using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Users.Create;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.UserFeatures;

public sealed class CreateUserRequest
{
    [Required]
    public string Name { get; set; } = String.Empty;

    [Required]
    public string Email { get; set; } = String.Empty;

    [Required]
    public string Password { get; set; } = String.Empty;
    public string? PhoneNumber { get; set; } = null;
}

public sealed record CreateUserResponse(int Id, string Name);

public class CreateEndpoint(IMediator _mediator)
    : Endpoint<
        CreateUserRequest,
        Results<Created<CreateUserResponse>, ValidationProblem, ProblemHttpResult>
    >
{
    public override void Configure()
    {
        Post("/users");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Create a new user";
            s.Description =
                "Creates a new user with the specified name, email, password, and phone number.";
            s.ExampleRequest = new CreateUserRequest
            {
                Name = "Sample User",
                Email = "sample.user@example.com",
                Password = "Passw0rd!",
            };
            s.ResponseExamples[201] = new CreateUserResponse(Id: 1, Name: "Teste");

            s.Responses[201] = "User created successfully";
            s.Responses[400] = "Invalid request data";
        });

        Tags("Users");

        Description(builder =>
            builder
                .Accepts<CreateUserRequest>()
                .Produces<CreateUserResponse>(201, "application/json")
                .ProducesProblem(400)
        );
    }

    public override async Task<
        Results<Created<CreateUserResponse>, ValidationProblem, ProblemHttpResult>
    > ExecuteAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateUserCommand(
            UserName.From(request.Name),
            new EmailAddress(request.Email),
            request.Password,
            request.PhoneNumber ?? String.Empty
        );
        var result = await _mediator.Send(command, cancellationToken);

        return result.ToCreatedResult(
            id => $"/users/{id}",
            id => new CreateUserResponse(id.Value, command.Name.Value)
        );
    }
}

public sealed class CreateUserValidator : Validator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MinimumLength(2)
            .MaximumLength(UserName.MaxLength)
            .WithMessage($"User name must not exceed {UserName.MaxLength} characters");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .Matches(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")
            .WithMessage("Email must be a valid email address");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required")
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters");
    }
}
