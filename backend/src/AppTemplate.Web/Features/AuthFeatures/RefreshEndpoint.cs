using AppTemplate.UseCases.Auth.Refresh;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.AuthFeatures;

public sealed class RefreshRequest
{
    public string RefreshToken { get; set; } = String.Empty;
}

public class RefreshEndpoint(IMediator _mediator)
    : Endpoint<
        RefreshRequest,
        Results<Ok<AuthTokensResponse>, ValidationProblem, ProblemHttpResult>
    >
{
    public override void Configure()
    {
        Post("/refresh");
        // Called precisely when the access token has expired, so this must stay reachable
        // without one.
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Exchange a refresh token for a new access/refresh token pair";
            s.Responses[200] = "New tokens issued";
            s.Responses[400] = "Refresh token is missing, invalid, expired, or already used";
        });

        Tags("Auth");

        Description(builder =>
            builder
                .Accepts<RefreshRequest>()
                .Produces<AuthTokensResponse>(200, "application/json")
                .ProducesProblem(400)
        );
    }

    public override async Task<
        Results<Ok<AuthTokensResponse>, ValidationProblem, ProblemHttpResult>
    > ExecuteAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var command = new RefreshCommand(request.RefreshToken);
        var result = await _mediator.Send(command, cancellationToken);

        return result.ToAuthResult(AuthTokensResponse.FromDto);
    }
}

public sealed class RefreshValidator : Validator<RefreshRequest>
{
    public RefreshValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Refresh token is required");
    }
}
