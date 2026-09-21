using AppTemplate.UseCases.Auth.Login;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.AuthFeatures;

public sealed class LoginRequest
{
    public string Email { get; set; } = String.Empty;
    public string Password { get; set; } = String.Empty;
}

public class LoginEndpoint(IMediator _mediator)
    : Endpoint<LoginRequest, Results<Ok<AuthTokensResponse>, ValidationProblem, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/login");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Log in with email and password";
            s.ExampleRequest = new LoginRequest
            {
                Email = "sample.user@example.com",
                Password = "Passw0rd!",
            };

            s.Responses[200] = "Logged in successfully";
            s.Responses[400] = "Invalid credentials";
        });

        Tags("Auth");

        Description(builder =>
            builder
                .Accepts<LoginRequest>()
                .Produces<AuthTokensResponse>(200, "application/json")
                .ProducesProblem(400)
        );
    }

    public override async Task<
        Results<Ok<AuthTokensResponse>, ValidationProblem, ProblemHttpResult>
    > ExecuteAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await _mediator.Send(command, cancellationToken);

        return result.ToAuthResult(AuthTokensResponse.FromDto);
    }
}

public sealed class LoginValidator : Validator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Email is required");

        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required");
    }
}
