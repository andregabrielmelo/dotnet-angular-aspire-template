using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Concurrency;
using AppTemplate.UseCases.Idempotency;

namespace AppTemplate.UseCases.Users.Update;

/// <param name="PhoneNumber">Leave empty to keep the current phone number.</param>
/// <param name="PhoneCountryCode">Required with <paramref name="PhoneNumber"/>, e.g. "+55".</param>
/// <param name="Precondition">From <c>If-Match</c>; without it, a concurrent change is a 409.</param>
/// <param name="IdempotencyKey">From <c>Idempotency-Key</c>: a repeat replays the first result.</param>
public record UpdateUserCommand(
    UserId UserId,
    UserName UserName,
    [property: PersonalData] string? PhoneNumber,
    [property: PersonalData] string? PhoneCountryCode,
    [property: PersonalData] string? PhoneExtension,
    VersionPrecondition? Precondition = null,
    string? IdempotencyKey = null
) : Mediator.ICommand<Result<UserDto>>, IIdempotentCommand
{
    public string Operation => "users.update.v1";
}
