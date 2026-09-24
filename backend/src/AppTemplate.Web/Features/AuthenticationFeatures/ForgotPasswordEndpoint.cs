using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Users.ForgotPassword;

namespace AppTemplate.Web.Features.AuthenticationFeatures;

public sealed class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Starts a password reset: Keycloak emails the user a link to its own "set a new password"
/// page. Always answers 202 for a well-formed email, whether or not an account exists, so it
/// can't be used to discover registered addresses.
/// </summary>
public sealed class ForgotPasswordEndpoint(IMediator _mediator)
    : Endpoint<ForgotPasswordRequest, Results<Accepted, ProblemHttpResult>>
{
    public const int RequestsPerWindow = 5;
    public const int WindowSeconds = 60;

    public override void Configure()
    {
        Post("/password-reset");
        AllowAnonymous();
        // Per client IP (X-Forwarded-For from the backend for frontend) - limits email flooding.
        Throttle(hitLimit: RequestsPerWindow, durationSeconds: WindowSeconds);

        Summary(s =>
        {
            s.Summary = "Request a password reset email";
            s.Description =
                "If an account uses the given email, Keycloak sends it a link to set a new password. "
                + "The response is the same whether or not the account exists.";
            s.ExampleRequest = new ForgotPasswordRequest { Email = "sample.user@example.com" };

            s.Responses[202] = "Request accepted; an email is sent if the account exists";
            s.Responses[400] = "Invalid email";
            s.Responses[429] = "Too many requests";
            s.Responses[503] = "The identity provider is unavailable";
        });

        Tags("Authentication");

        Description(builder =>
            builder
                .Accepts<ForgotPasswordRequest>("application/json")
                .Produces(202)
                .ProducesProblem(400)
                .ProducesProblem(429)
                .ProducesProblem(503)
        );
    }

    public override async Task<Results<Accepted, ProblemHttpResult>> ExecuteAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new ForgotPasswordCommand(new EmailAddress(request.Email.Trim())),
            cancellationToken
        );

        return result.Status switch
        {
            ResultStatus.Ok => TypedResults.Accepted((string?)null),
            ResultStatus.Unavailable => TypedResults.Problem(
                title: "Service unavailable",
                detail: "Password reset is temporarily unavailable. Please try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable
            ),
            _ => TypedResults.Problem(
                title: "Password reset failed",
                detail: "The password reset email could not be sent. Please try again later.",
                statusCode: StatusCodes.Status500InternalServerError
            ),
        };
    }
}

public sealed class ForgotPasswordValidator : Validator<ForgotPasswordRequest>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .MaximumLength(320)
            .Matches(@"^\s*[^@\s]+@[^@\s]+\.[^@\s]+\s*$")
            .WithMessage("Email must be a valid email address");
    }
}
