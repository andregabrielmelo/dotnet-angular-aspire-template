using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Users.Update;

public record UpdateUserCommand(UserId UserId, UserName UserName, string? PhoneNumber)
    : Mediator.ICommand<Result<UserDto>>;
