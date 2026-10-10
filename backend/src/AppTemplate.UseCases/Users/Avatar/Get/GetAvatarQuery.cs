using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Users.Avatar.Get;

/// <param name="CachedKey">The key the client already has (from <c>If-None-Match</c>), if any.</param>
public sealed record GetAvatarQuery(UserId UserId, string? CachedKey = null)
    : IQuery<Result<AvatarDto>>;

/// <param name="Key">The object key; unique per upload, so it doubles as a strong ETag.</param>
/// <param name="File">Null when the client's cached copy is current (no need to read storage).</param>
public sealed record AvatarDto(string Key, StoredFile? File);
