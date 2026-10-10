using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Authorization;

namespace AppTemplate.UseCases.Users.Avatar.Get;

/// <summary>Your own avatar, or anyone's with <see cref="Permission.UsersRead"/> (as for profiles).</summary>
public sealed class GetAvatarHandler(
    IRepository<User> _repository,
    ICurrentUser _currentUser,
    IFileStorage _storage
) : IQueryHandler<GetAvatarQuery, Result<AvatarDto>>
{
    public async ValueTask<Result<AvatarDto>> Handle(
        GetAvatarQuery query,
        CancellationToken cancellationToken
    )
    {
        var user = await _repository.GetByIdAsync(query.UserId, cancellationToken);
        if (user is null)
        {
            return Result.NotFound();
        }
        var isOwn =
            _currentUser.ExternalId is not null && _currentUser.ExternalId == user.ExternalId;
        if (!isOwn && !_currentUser.HasPermission(Permission.UsersRead))
        {
            return Result.Forbidden();
        }
        if (user.AvatarKey is null)
        {
            return Result.NotFound("This user has no avatar.");
        }

        if (query.CachedKey == user.AvatarKey)
        {
            return new AvatarDto(user.AvatarKey, null);
        }

        try
        {
            var file = await _storage.GetAsync(user.AvatarKey, cancellationToken);
            return file is null
                ? Result.NotFound("The avatar file is missing.")
                : new AvatarDto(user.AvatarKey, file);
        }
        catch (FileStorageUnavailableException)
        {
            return Result.Unavailable("File storage is unavailable. Try again later.");
        }
    }
}
