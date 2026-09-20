using AppTemplate.UseCases.Auth.ResetPassword;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.AuthFeatures;

public sealed class ResetPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class ResetPasswordEndpoint(IMediator _mediator)
    : Endpoint<
        ResetPasswordRequest,
        Results<NoContent, ValidationProblem, NotFound, ProblemHttpResult>
    >
{
    public override void Configure()
    {
        Post("/auth/reset-password");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Reset a password using a token from a forgot-password email";
            s.Description = "Signs out every other outstanding session as a side effect.";
            s.ExampleRequest = new ResetPasswordRequest
            {
                Token = "...",
                NewPassword = "NewPassw0rd!",
            };
            s.Responses[204] = "Password reset";
            s.Responses[400] = "Invalid or expired token";
        });

        Tags("Auth");

        Description(builder =>
            builder.Accepts<ResetPasswordRequest>().Produces(204).ProducesProblem(400)
        );
    }

    public override async Task<
        Results<NoContent, ValidationProblem, NotFound, ProblemHttpResult>
    > ExecuteAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var command = new ResetPasswordCommand(request.Token, request.NewPassword);
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToNoContentResult();
    }
}

public sealed class ResetPasswordValidator : Validator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("Token is required");
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("New password is required")
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters");
    }
}
