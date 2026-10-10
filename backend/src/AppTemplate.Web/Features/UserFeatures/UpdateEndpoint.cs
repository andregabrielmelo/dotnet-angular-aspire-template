using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Users;
using AppTemplate.UseCases.Users.Update;
using AppTemplate.Web.Configurations;
using AppTemplate.Web.Extensions;
using AppTemplate.Web.Http;
using AppTemplate.Web.Validation;

namespace AppTemplate.Web.Features.UserFeatures;

public sealed class UpdateUserRequest
{
    /// <summary>Bound from the route - FastEndpoints lets route values override the body.</summary>
    public int Id { get; set; }

    public required string Name { get; set; }

    /// <summary>Leave empty to keep the current phone number.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>E.164 country calling code, e.g. "+55". Required with <see cref="PhoneNumber"/>.</summary>
    public string? PhoneCountryCode { get; set; }

    public string? PhoneExtension { get; set; }

    /// <summary>
    /// Optional. Repeating the request with the same key (within 24 hours) replays the first
    /// result instead of updating again; the same key with a different body is 422.
    /// </summary>
    [FromHeader("Idempotency-Key", IsRequired = false)]
    public string? IdempotencyKey { get; set; }
}

public sealed record UpdateUserResponse(UserRecord User);

public class UpdateEndpoint(IMediator _mediator)
    : Endpoint<
        UpdateUserRequest,
        Results<Ok<UpdateUserResponse>, ProblemHttpResult>,
        UpdateUserMapper
    >
{
    public override void Configure()
    {
        Put("/users/{id}");
        Version(ApiVersions.V1);

        Summary(s =>
        {
            s.Summary = "Update a user";
            s.Description =
                "Updates a user's name and, optionally, phone number (which needs a country code). "
                + "Users may update their own profile; "
                + "updating anyone else's requires the users:write permission. "
                + "Send the ETag from GET in If-Match to update only if the user is unchanged since: "
                + "a stale or weak tag gets 412, * matches any existing user. Without If-Match, a "
                + "change that races another write gets 409. The response's ETag is the new version.";
            s.ExampleRequest = new UpdateUserRequest
            {
                Id = 1,
                Name = "Sample User",
                PhoneNumber = "555 0100",
                PhoneCountryCode = "+1",
            };
            s.ResponseExamples[200] = new UpdateUserResponse(
                new UserRecord(1, "Sample User", "+1 555 0100")
            );

            s.Responses[200] = "User updated successfully";
            s.Responses[400] = "Invalid request data";
            s.Responses[403] = "Not your profile, and no users:write permission";
            s.Responses[404] = "User not found";
            s.Responses[409] = "Changed by someone else while saving (no If-Match sent)";
            s.Responses[412] = "If-Match doesn't match the current version";
            s.Responses[422] = "Idempotency-Key reused with a different request";
        });

        Tags("Users");

        Description(builder =>
            builder
                .Accepts<UpdateUserRequest>()
                .Produces<UpdateUserResponse>(200, "application/json")
                .ProducesProblem(400)
                .ProducesProblem(403)
                .ProducesProblem(404)
                .ProducesProblem(409)
                .ProducesProblem(412)
                .ProducesProblem(422)
        );
    }

    public override async Task<Results<Ok<UpdateUserResponse>, ProblemHttpResult>> ExecuteAsync(
        UpdateUserRequest request,
        CancellationToken cancellationToken
    )
    {
        var ifMatch = EntityTagHeader.ParseIfMatch(HttpContext.Request.Headers.IfMatch);
        if (ifMatch.IsMalformed)
        {
            return TypedResults.Problem(
                ProblemDetailsConfigurations.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["If-Match"] = ["If-Match must be * or a list of quoted entity tags."],
                    },
                    HttpContext
                )
            );
        }

        var command = new UpdateUserCommand(
            UserId.From(request.Id),
            UserName.From(request.Name),
            request.PhoneNumber,
            request.PhoneCountryCode,
            request.PhoneExtension,
            ifMatch.Precondition,
            request.IdempotencyKey
        );
        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsSuccess)
        {
            HttpContext.Response.Headers.ETag = EntityTagHeader.Format(result.Value.Version);
        }

        return result.ToOkResult<UserDto, UpdateUserResponse>(Map.FromEntity);
    }
}

public sealed class UpdateUserValidator : Validator<UpdateUserRequest>
{
    public const int IdempotencyKeyMaxLength = 64;

    public UpdateUserValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MinimumLength(2)
            .MaximumLength(UserName.MaxLength)
            .WithMessage($"User name must not exceed {UserName.MaxLength} characters");
        // Id comes from the route (route values override the body), so there is no separate
        // body Id to compare it against.
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("Id must be greater than zero");
        RuleFor(x => x.PhoneCountryCode)
            .NotEmpty()
            .WithMessage("Phone country code is required with a phone number")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
        RuleFor(x => x.PhoneCountryCode).PhoneCountryCode();
        RuleFor(x => x.PhoneNumber).PhoneNumber();
        RuleFor(x => x.PhoneExtension).PhoneExtension();
        RuleFor(x => x.IdempotencyKey)
            .Length(1, IdempotencyKeyMaxLength)
            .Matches("^[A-Za-z0-9_-]+$")
            .WithMessage(
                $"Idempotency-Key must be 1 to {IdempotencyKeyMaxLength} letters, digits, '-' or '_' (a UUID works)."
            )
            .When(x => x.IdempotencyKey is not null);
    }
}

public sealed class UpdateUserMapper : Mapper<UpdateUserRequest, UpdateUserResponse, UserDto>
{
    public override UpdateUserResponse FromEntity(UserDto e) =>
        new(new UserRecord(e.Id.Value, e.Name.Value, e.PhoneNumber?.ToString()));
}
