namespace AppTemplate.Infrastructure.Auth;

/// <summary>
/// Bound from "Authentication:Jwt". SigningKey is a secret - set via user-secrets/environment
/// variables, never committed. Shared between JwtTokenService (mints tokens) and Web's
/// AddJwtBearer setup (validates them), so both sides agree on issuer/audience/key.
/// </summary>
public class JwtOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
    public int RefreshTokenLifetimeDays { get; set; } = 14;
}
