using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Users;

/// <summary>The signed-in caller's own profile.</summary>
public record CurrentUserDto(UserId Id, UserName Name, EmailAddress Email)
{
    public static CurrentUserDto FromEntity(User user) => new(user.Id, user.Name, user.Email);
}
