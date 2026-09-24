using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Caching;

namespace AppTemplate.UseCases.Users.Update;

public record UpdateUserCommand(UserId UserId, UserName UserName, string? PhoneNumber)
    : Mediator.ICommand<Result<UserDto>>;

/// <summary>
/// Resource-based authorization: anyone may update their own profile, while updating someone
/// else's requires <see cref="Permission.UsersWrite"/>. This needs the loaded user, so it is
/// decided here rather than by an endpoint policy.
/// </summary>
public class UpdateUserHandler(
    IRepository<User> _repository,
    ICurrentUser _currentUser,
    ICacheInvalidator _cacheInvalidator
) : Mediator.ICommandHandler<UpdateUserCommand, Result<UserDto>>
{
    public async ValueTask<Result<UserDto>> Handle(
        UpdateUserCommand command,
        CancellationToken cancellationToken
    )
    {
        var user = await _repository.GetByIdAsync(command.UserId, cancellationToken);
        if (user == null)
            return Result<UserDto>.NotFound();

        var isOwnProfile =
            _currentUser.ExternalId is not null && _currentUser.ExternalId == user.ExternalId;
        if (!isOwnProfile && !_currentUser.HasPermission(Permission.UsersWrite))
            return Result<UserDto>.Forbidden();

        user.UpdateName(command.UserName);
        if (!string.IsNullOrEmpty(command.PhoneNumber))
        {
            var phoneNumber = new PhoneNumber("+1", command.PhoneNumber, String.Empty);
            user.UpdatePhoneNumber(phoneNumber);
        }

        await _repository.UpdateAsync(user, cancellationToken);
        await _cacheInvalidator.InvalidateAsync(CacheTags.Users, cancellationToken);

        var dto = new UserDto(user.Id, user.Name, user.PhoneNumber);
        return Result<UserDto>.Success(dto);
    }
}
