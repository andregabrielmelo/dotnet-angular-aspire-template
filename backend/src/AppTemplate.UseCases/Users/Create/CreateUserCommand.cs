using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Users.Create;

public record CreateUserCommand(
    UserName Name,
    EmailAddress Email,
    string Password,
    string PhoneNumber
) : ICommand<Result<UserId>>;
