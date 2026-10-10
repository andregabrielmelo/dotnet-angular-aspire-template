using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Users;

/// <param name="Version">The user row's version (the ETag); 0 where it isn't read, such as list pages.</param>
public record UserDto(UserId Id, UserName Name, PhoneNumber? PhoneNumber, uint Version = 0);
