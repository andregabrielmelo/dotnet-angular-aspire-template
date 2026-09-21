using AppTemplate.UseCases.Auth.Logout;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.AuthFeatures;

public sealed class LogoutRequest
{
    public string RefreshToken { get; set; } = String.Empty;
}

public class LogoutEndpoint(IMediator _mediator)
    : Endpoint<LogoutRequest, Results<NoContent, ValidationProblem, NotFound, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/logout");
        // Revocation is authorized by possession of the refresh token itself, not by a bearer
        // access token - this must stay reachable even after the access token has expired.
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Revoke a refresh token";
            s.Description =
                "Idempotent - revoking an already-revoked or unknown token still returns success, "
                + "so this call can never be used to probe whether a token exists or is valid.";
            s.Responses[204] = "Refresh token revoked";
        });

        Tags("Auth");

        Description(builder => builder.Accepts<LogoutRequest>().ProducesProblem(400));
    }

    public override async Task<
        Results<NoContent, ValidationProblem, NotFound, ProblemHttpResult>
    > ExecuteAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        var command = new LogoutCommand(request.RefreshToken);
        var result = await _mediator.Send(command, cancellationToken);

        return result.ToNoContentResult();
    }
}

public sealed class LogoutValidator : Validator<LogoutRequest>
{
    public LogoutValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Refresh token is required");
    }
}
