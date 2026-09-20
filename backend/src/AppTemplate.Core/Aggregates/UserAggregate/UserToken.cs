namespace AppTemplate.Core.Aggregates.UserAggregate;

/// <summary>
/// A single-use, hashed token for email confirmation or password reset. Child entity of User.
/// Only the hash is ever stored - the raw token is emailed to the user and never persisted.
/// </summary>
public class UserToken(
    UserId userId,
    UserTokenPurpose purpose,
    string tokenHash,
    DateTimeOffset expiresAtUtc
) : EntityBase<int>
{
    public UserId UserId { get; private set; } = userId;
    public UserTokenPurpose Purpose { get; private set; } = purpose;
    public string TokenHash { get; private set; } = tokenHash;
    public DateTimeOffset ExpiresAtUtc { get; private set; } = expiresAtUtc;
    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public bool IsValid(DateTimeOffset now) => ConsumedAtUtc is null && ExpiresAtUtc > now;

    public void MarkConsumed(DateTimeOffset now) => ConsumedAtUtc = now;
}
