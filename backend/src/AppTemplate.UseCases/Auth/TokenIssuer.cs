using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Interfaces;

namespace AppTemplate.UseCases.Auth;

/// <summary>Shared by LoginHandler and ExternalLoginHandler so both issue token pairs identically.</summary>
internal static class TokenIssuer
{
    public static AuthResultDto IssueTokenPair(User user, IJwtTokenService tokenService)
    {
        var accessToken = tokenService.CreateAccessToken(user.Id, user.Name, user.Email);

        var rawRefreshToken = TokenHasher.GenerateRawToken();
        user.AddRefreshToken(
            TokenHasher.Hash(rawRefreshToken),
            DateTimeOffset.UtcNow.Add(tokenService.RefreshTokenLifetime)
        );

        return new AuthResultDto(user.Id, user.Name, user.Email, accessToken, rawRefreshToken);
    }
}
