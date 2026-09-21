using AppTemplate.UseCases.Auth;

namespace AppTemplate.Web.Features.AuthFeatures;

public sealed record AuthTokensResponse(
    int UserId,
    string Name,
    string Email,
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc
)
{
    public static AuthTokensResponse FromDto(AuthTokensDto dto) =>
        new(
            dto.UserId,
            dto.Name,
            dto.Email,
            dto.AccessToken,
            dto.AccessTokenExpiresAtUtc,
            dto.RefreshToken,
            dto.RefreshTokenExpiresAtUtc
        );
}
