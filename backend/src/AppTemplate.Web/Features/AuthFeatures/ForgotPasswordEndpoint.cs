using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Auth.ForgotPassword;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.AuthFeatures;

public sealed class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ForgotPasswordEndpoint(IMediator _mediator)
    : Endpoint<
        ForgotPasswordRequest,
        Results<NoContent, ValidationProblem, NotFound, ProblemHttpResult>
    >
{
    public override void Configure()
    {
        Post("/auth/forgot-password");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Request a password reset email";
            s.Description =
                "Always returns 204, whether or not the email is registered, to avoid leaking account existence.";
            s.ExampleRequest = new ForgotPasswordRequest { Email = "sample.user@example.com" };
            s.Responses[204] = "If the email is registered, a reset link was sent";
        });

        Tags("Auth");

        Description(builder => builder.Accepts<ForgotPasswordRequest>().Produces(204));
    }

    public override async Task<
        Results<NoContent, ValidationProblem, NotFound, ProblemHttpResult>
    > ExecuteAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var command = new ForgotPasswordCommand(new EmailAddress(request.Email));
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToNoContentResult();
    }
}

public sealed class ForgotPasswordValidator : Validator<ForgotPasswordRequest>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Email must be a valid email address");
    }
}
