using AppTemplate.Core.ValueObjects;
using AppTemplate.Infrastructure.Auth;
using AppTemplate.UseCases.Auth.Login;
using AppTemplate.Web.Extensions;
using Microsoft.Extensions.Options;

namespace AppTemplate.Web.Features.AuthFeatures;

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginEndpoint(IMediator _mediator, IOptions<JwtOptions> _jwtOptions)
    : Endpoint<LoginRequest, Results<Ok<AuthTokenResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/auth/login");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Log in with email and password";
            s.Description =
                "Returns a short-lived access token and a longer-lived, rotating refresh token.";
            s.ExampleRequest = new LoginRequest
            {
                Email = "sample.user@example.com",
                Password = "Passw0rd!",
            };

            s.Responses[200] = "Login succeeded";
            s.Responses[401] = "Invalid credentials";
            s.Responses[403] = "Email not confirmed";
        });

        Tags("Auth");

        Description(builder =>
            builder
                .Accepts<LoginRequest>()
                .Produces<AuthTokenResponse>(200, "application/json")
                .ProducesProblem(401)
                .ProducesProblem(403)
        );
    }

    public override async Task<Results<Ok<AuthTokenResponse>, ProblemHttpResult>> ExecuteAsync(
        LoginRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new LoginCommand(new EmailAddress(request.Email), request.Password);
        var result = await _mediator.Send(command, cancellationToken);

        return result.ToAuthResult(dto =>
            AuthTokenResponse.FromResult(dto, _jwtOptions.Value.AccessTokenLifetimeMinutes)
        );
    }
}

public sealed class LoginValidator : Validator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Email must be a valid email address");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required");
    }
}
