using AppTemplate.UseCases.Auth;

namespace AppTemplate.Web.Features.AuthFeatures;

public sealed record AuthUserResponse(int Id, string Name, string Email);

public sealed record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    AuthUserResponse User
)
{
    public static AuthTokenResponse FromResult(AuthResultDto dto, int accessTokenLifetimeMinutes) =>
        new(
            dto.AccessToken,
            dto.RefreshToken,
            accessTokenLifetimeMinutes * 60,
            new AuthUserResponse(dto.Id.Value, dto.Name.Value, dto.Email.Value)
        );
}
