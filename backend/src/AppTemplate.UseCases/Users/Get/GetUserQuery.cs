using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Users.Get;

public record GetUserQuery(UserId UserId) : IQuery<Result<UserDto>>;
