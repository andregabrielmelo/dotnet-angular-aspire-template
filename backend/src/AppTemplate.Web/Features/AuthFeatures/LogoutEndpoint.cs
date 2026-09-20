using AppTemplate.UseCases.Auth.Logout;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.AuthFeatures;

public sealed class LogoutRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class LogoutEndpoint(IMediator _mediator)
    : Endpoint<LogoutRequest, Results<NoContent, ValidationProblem, NotFound, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/auth/logout");
        AllowAnonymous(); // possession of a valid refresh token is what authorizes the revoke

        Summary(s =>
        {
            s.Summary = "Log out";
            s.Description = "Revokes the given refresh token.";
            s.ExampleRequest = new LogoutRequest { RefreshToken = "..." };
            s.Responses[204] = "Logged out";
        });

        Tags("Auth");

        Description(builder => builder.Accepts<LogoutRequest>().Produces(204));
    }

    public override async Task<
        Results<NoContent, ValidationProblem, NotFound, ProblemHttpResult>
    > ExecuteAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new LogoutCommand(request.RefreshToken),
            cancellationToken
        );
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
