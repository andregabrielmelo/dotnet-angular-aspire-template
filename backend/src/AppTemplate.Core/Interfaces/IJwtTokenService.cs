using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.Core.Interfaces;

public interface IJwtTokenService
{
    string CreateAccessToken(UserId id, UserName name, EmailAddress email);

    /// <summary>How long a freshly-issued refresh token should remain valid for.</summary>
    TimeSpan RefreshTokenLifetime { get; }
}
