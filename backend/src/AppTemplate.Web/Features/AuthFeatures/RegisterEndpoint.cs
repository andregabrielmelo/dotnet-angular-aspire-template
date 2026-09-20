using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Auth.Register;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.AuthFeatures;

public sealed class RegisterRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}

public sealed record RegisterResponse(int Id, string Name);

public class RegisterEndpoint(IMediator _mediator)
    : Endpoint<
        RegisterRequest,
        Results<Created<RegisterResponse>, ValidationProblem, ProblemHttpResult>
    >
{
    public override void Configure()
    {
        Post("/auth/register");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Register a new account";
            s.Description =
                "Creates a new account and emails a confirmation link. Does not log the user in - confirm the email, then log in separately.";
            s.ExampleRequest = new RegisterRequest
            {
                Name = "Sample User",
                Email = "sample.user@example.com",
                Password = "Passw0rd!",
            };
            s.ResponseExamples[201] = new RegisterResponse(Id: 1, Name: "Sample User");

            s.Responses[201] = "Account created; confirmation email sent";
            s.Responses[400] = "Invalid request data";
        });

        Tags("Auth");

        Description(builder =>
            builder
                .Accepts<RegisterRequest>()
                .Produces<RegisterResponse>(201, "application/json")
                .ProducesProblem(400)
        );
    }

    public override async Task<
        Results<Created<RegisterResponse>, ValidationProblem, ProblemHttpResult>
    > ExecuteAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterCommand(
            UserName.From(request.Name),
            new EmailAddress(request.Email),
            request.Password,
            request.PhoneNumber
        );
        var result = await _mediator.Send(command, cancellationToken);

        return result.ToCreatedResult(
            id => $"/users/{id}",
            id => new RegisterResponse(id.Value, command.Name.Value)
        );
    }
}

public sealed class RegisterValidator : Validator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MinimumLength(2)
            .MaximumLength(UserName.MaxLength)
            .WithMessage($"Name must not exceed {UserName.MaxLength} characters");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Email must be a valid email address");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required")
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters");
    }
}
