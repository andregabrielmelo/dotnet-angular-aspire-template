namespace AppTemplate.Core.Aggregates.UserAggregate;

/// <summary>
/// An opaque, hashed refresh token used to mint new access tokens. Child entity of User.
/// Rotated on every use (ReplacedByTokenHash) so a reused, already-revoked token is a signal
/// of theft - see RefreshTokenHandler for the reuse-detection behavior.
/// </summary>
public class RefreshToken(
    UserId userId,
    string tokenHash,
    DateTimeOffset expiresAtUtc,
    DateTimeOffset createdAtUtc
) : EntityBase<int>
{
    public UserId UserId { get; private set; } = userId;
    public string TokenHash { get; private set; } = tokenHash;
    public DateTimeOffset ExpiresAtUtc { get; private set; } = expiresAtUtc;
    public DateTimeOffset CreatedAtUtc { get; private set; } = createdAtUtc;
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAtUtc is null && ExpiresAtUtc > now;

    public void Revoke(DateTimeOffset now, string? replacedByTokenHash = null)
    {
        RevokedAtUtc = now;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
