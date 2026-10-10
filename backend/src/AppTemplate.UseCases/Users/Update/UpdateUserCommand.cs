using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Concurrency;

namespace AppTemplate.UseCases.Users.Update;

/// <param name="PhoneNumber">Leave empty to keep the current phone number.</param>
/// <param name="PhoneCountryCode">Required with <paramref name="PhoneNumber"/>, e.g. "+55".</param>
/// <param name="Precondition">From <c>If-Match</c>; without it, a concurrent change is a 409.</param>
public record UpdateUserCommand(
    UserId UserId,
    UserName UserName,
    [property: PersonalData] string? PhoneNumber,
    [property: PersonalData] string? PhoneCountryCode,
    [property: PersonalData] string? PhoneExtension,
    VersionPrecondition? Precondition = null
) : Mediator.ICommand<Result<UserDto>>;
