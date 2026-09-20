using AppTemplate.Infrastructure.Auth;
using AppTemplate.UseCases.Auth.RefreshToken;
using AppTemplate.Web.Extensions;
using Microsoft.Extensions.Options;

namespace AppTemplate.Web.Features.AuthFeatures;

public sealed class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class RefreshEndpoint(IMediator _mediator, IOptions<JwtOptions> _jwtOptions)
    : Endpoint<RefreshRequest, Results<Ok<AuthTokenResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/auth/refresh");
        AllowAnonymous(); // the refresh token itself is the credential

        Summary(s =>
        {
            s.Summary = "Exchange a refresh token for a new access/refresh token pair";
            s.Description =
                "Rotates the refresh token on every use. Presenting an already-rotated-away token revokes every outstanding session (reuse detection).";
            s.ExampleRequest = new RefreshRequest { RefreshToken = "..." };

            s.Responses[200] = "A new token pair";
            s.Responses[401] = "Refresh token is invalid, expired, or already used";
        });

        Tags("Auth");

        Description(builder =>
            builder
                .Accepts<RefreshRequest>()
                .Produces<AuthTokenResponse>(200, "application/json")
                .ProducesProblem(401)
        );
    }

    public override async Task<Results<Ok<AuthTokenResponse>, ProblemHttpResult>> ExecuteAsync(
        RefreshRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new RefreshTokenCommand(request.RefreshToken);
        var result = await _mediator.Send(command, cancellationToken);

        return result.ToAuthResult(dto =>
            AuthTokenResponse.FromResult(dto, _jwtOptions.Value.AccessTokenLifetimeMinutes)
        );
    }
}

public sealed class RefreshValidator : Validator<RefreshRequest>
{
    public RefreshValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Refresh token is required");
    }
}
