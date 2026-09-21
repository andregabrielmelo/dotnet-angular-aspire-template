namespace AppTemplate.Infrastructure.Identity;

/// <summary>
/// An opaque, rotating refresh token. Only <see cref="TokenHash"/> (SHA-256 of the raw token
/// handed to the client) is ever persisted - the raw value is never stored.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }

    public required string ApplicationUserId { get; set; }
    public ApplicationUser? ApplicationUser { get; set; }

    public required string TokenHash { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;
}
