using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Auth;

/// <summary>
/// Owns credential storage and token issuance (implemented in Infrastructure on top of
/// ASP.NET Core Identity). Kept out of Core/UseCases proper so those layers never take a
/// dependency on Identity's framework types - see ADR-001.
/// </summary>
public interface IIdentityService
{
    /// <summary>
    /// Persists both the given (not-yet-saved) domain <see cref="User"/> and its Identity
    /// credentials in one transaction, then logs the new user in.
    /// </summary>
    Task<Result<AuthTokensDto>> RegisterAsync(
        User newDomainUser,
        string password,
        CancellationToken cancellationToken
    );

    Task<Result<AuthTokensDto>> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken
    );

    Task<Result<AuthTokensDto>> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken
    );

    /// <summary>Idempotent: revoking an already-revoked or unknown token still succeeds.</summary>
    Task<Result> LogoutAsync(string refreshToken, CancellationToken cancellationToken);
}
