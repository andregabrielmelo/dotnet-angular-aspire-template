using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Interfaces;
using AppTemplate.Core.ValueObjects;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AppTemplate.Infrastructure.Auth;

/// <summary>
/// Claims are written using the ClaimTypes.* constants (not the short JWT-standard names like
/// "sub") so the same constants can be used to read them back off HttpContext.User with no
/// inbound claim-type remapping to reason about.
/// </summary>
public class JwtTokenService(IOptions<JwtOptions> jwtOptions) : IJwtTokenService
{
    private readonly JwtOptions _options = jwtOptions.Value;

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenLifetimeDays);

    public string CreateAccessToken(UserId id, UserName name, EmailAddress email)
    {
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, id.Value.ToString()),
            new(ClaimTypes.Name, name.Value),
            new(ClaimTypes.Email, email.Value),
        ];

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenLifetimeMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
