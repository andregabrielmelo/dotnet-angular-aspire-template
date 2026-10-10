using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Idempotency;

namespace AppTemplate.UseCases.Users.Update;

/// <summary>
/// The same rule as <see cref="UpdateUserHandler"/> (your own profile, or anyone's with
/// <see cref="Permission.UsersWrite"/>), checked before an idempotent replay so losing the
/// permission also stops replays.
/// </summary>
public sealed class UpdateUserAuthorizer(IRepository<User> _repository, ICurrentUser _currentUser)
    : ICommandAuthorizer<UpdateUserCommand>
{
    public async ValueTask<bool> IsAllowedAsync(
        UpdateUserCommand command,
        CancellationToken cancellationToken
    )
    {
        if (_currentUser.HasPermission(Permission.UsersWrite))
        {
            return true;
        }
        var user = await _repository.GetByIdAsync(command.UserId, cancellationToken);
        return user is null
            || (_currentUser.ExternalId is not null && user.ExternalId == _currentUser.ExternalId);
    }
}
