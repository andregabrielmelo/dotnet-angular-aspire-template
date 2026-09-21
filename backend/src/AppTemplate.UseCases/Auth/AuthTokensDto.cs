namespace AppTemplate.UseCases.Auth;

public record AuthTokensDto(
    int UserId,
    string Name,
    string Email,
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc
);
