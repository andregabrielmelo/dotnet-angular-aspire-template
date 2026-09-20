using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Auth;

public record AuthResultDto(
    UserId Id,
    UserName Name,
    EmailAddress Email,
    string AccessToken,
    string RefreshToken
);
