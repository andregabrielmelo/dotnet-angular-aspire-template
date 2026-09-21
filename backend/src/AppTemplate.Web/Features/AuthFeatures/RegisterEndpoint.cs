using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Auth.Register;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.AuthFeatures;

public sealed class RegisterRequest
{
    public string Name { get; set; } = String.Empty;
    public string Email { get; set; } = String.Empty;
    public string Password { get; set; } = String.Empty;
    public string? PhoneNumber { get; set; } = null;
}

public class RegisterEndpoint(IMediator _mediator)
    : Endpoint<
        RegisterRequest,
        Results<Ok<AuthTokensResponse>, ValidationProblem, ProblemHttpResult>
    >
{
    public override void Configure()
    {
        Post("/register");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Register a new account";
            s.Description =
                "Creates a new user profile and Identity credentials in one step, then logs the new user in.";
            s.ExampleRequest = new RegisterRequest
            {
                Name = "Sample User",
                Email = "sample.user@example.com",
                Password = "Passw0rd!",
            };

            s.Responses[200] = "Registered and logged in successfully";
            s.Responses[400] = "Invalid request data, or the email is already registered";
        });

        Tags("Auth");

        Description(builder =>
            builder
                .Accepts<RegisterRequest>()
                .Produces<AuthTokensResponse>(200, "application/json")
                .ProducesProblem(400)
        );
    }

    public override async Task<
        Results<Ok<AuthTokensResponse>, ValidationProblem, ProblemHttpResult>
    > ExecuteAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterCommand(
            UserName.From(request.Name),
            new EmailAddress(request.Email),
            request.Password,
            request.PhoneNumber ?? String.Empty
        );
        var result = await _mediator.Send(command, cancellationToken);

        return result.ToAuthResult(AuthTokensResponse.FromDto);
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
