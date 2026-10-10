using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Caching;
using AppTemplate.UseCases.Files;
using AppTemplate.UseCases.Telemetry;

namespace AppTemplate.UseCases.Users.Avatar.Set;

/// <summary>
/// Only the user themselves may replace their avatar: there's no permission that overrides it,
/// because an image under someone's name is theirs to choose. The upload is re-encoded before
/// it's stored (see <see cref="IImageProcessor"/>), under a fresh opaque key, and the old
/// object is released through the outbox.
/// </summary>
public sealed class SetAvatarHandler(
    IRepository<User> _repository,
    ICurrentUser _currentUser,
    IImageProcessor _images,
    IFileStorage _storage,
    ICacheInvalidator _cacheInvalidator,
    ApplicationMetrics _metrics
) : ICommandHandler<SetAvatarCommand, Result>
{
    public const string ContentPrefix = "avatars/";

    public async ValueTask<Result> Handle(
        SetAvatarCommand command,
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

        var processed = await _images.ProcessAvatarAsync(command.Upload, cancellationToken);
        if (!processed.IsSuccess)
        {
            _metrics.FileRejected(
                processed.ValidationErrors.FirstOrDefault()?.ErrorCode ?? "invalid"
            );
            return Result.Invalid(processed.ValidationErrors);
        }

        var key = ContentPrefix + Guid.CreateVersion7().ToString("N");
        try
        {
            await _storage.PutAsync(
                key,
                processed.Value.Content,
                processed.Value.ContentType,
                cancellationToken
            );
        }
        catch (FileStorageUnavailableException)
        {
            return Result.Unavailable("File storage is unavailable. Try again later.");
        }
        _metrics.FileUploaded(processed.Value.Content.Length);

        user.SetAvatar(key);
        await _repository.UpdateAsync(user, cancellationToken);
        await _cacheInvalidator.InvalidateAsync(CacheTags.Users, cancellationToken);
        return Result.Success();
    }
}
