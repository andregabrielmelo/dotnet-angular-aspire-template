using AppTemplate.UseCases.Auth.ConfirmEmail;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.AuthFeatures;

public sealed class ConfirmEmailRequest
{
    public string Token { get; set; } = string.Empty;
}

public class ConfirmEmailEndpoint(IMediator _mediator)
    : Endpoint<
        ConfirmEmailRequest,
        Results<NoContent, ValidationProblem, NotFound, ProblemHttpResult>
    >
{
    public override void Configure()
    {
        Post("/auth/confirm-email");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Confirm an email address";
            s.Description =
                "The token comes from the confirmation link emailed on registration - the frontend page reads it from the URL and posts it here.";
            s.ExampleRequest = new ConfirmEmailRequest { Token = "..." };
            s.Responses[204] = "Email confirmed";
            s.Responses[400] = "Invalid or expired token";
        });

        Tags("Auth");

        Description(builder =>
            builder.Accepts<ConfirmEmailRequest>().Produces(204).ProducesProblem(400)
        );
    }

    public override async Task<
        Results<NoContent, ValidationProblem, NotFound, ProblemHttpResult>
    > ExecuteAsync(ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ConfirmEmailCommand(request.Token),
            cancellationToken
        );
        return result.ToNoContentResult();
    }
}

public sealed class ConfirmEmailValidator : Validator<ConfirmEmailRequest>
{
    public ConfirmEmailValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("Token is required");
    }
}
