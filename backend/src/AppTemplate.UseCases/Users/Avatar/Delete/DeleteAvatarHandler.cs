using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Caching;

namespace AppTemplate.UseCases.Users.Avatar.Delete;

/// <summary>Owner only, like setting it. The object itself is deleted through the outbox.</summary>
public sealed class DeleteAvatarHandler(
    IRepository<User> _repository,
    ICurrentUser _currentUser,
    ICacheInvalidator _cacheInvalidator
) : ICommandHandler<DeleteAvatarCommand, Result>
{
    public async ValueTask<Result> Handle(
        DeleteAvatarCommand command,
        CancellationToken cancellationToken
    )
    {
        var user = await _repository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.NotFound();
        }
        if (_currentUser.ExternalId is null || _currentUser.ExternalId != user.ExternalId)
        {
            return Result.Forbidden();
        }
        if (user.AvatarKey is null)
        {
            return Result.NoContent();
        }

        user.RemoveAvatar();
        await _repository.UpdateAsync(user, cancellationToken);
        await _cacheInvalidator.InvalidateAsync(CacheTags.Users, cancellationToken);
        return Result.NoContent();
    }
}
