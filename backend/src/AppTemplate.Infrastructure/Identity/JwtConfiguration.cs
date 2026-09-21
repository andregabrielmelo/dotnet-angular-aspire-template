namespace AppTemplate.Infrastructure.Identity;

public class JwtConfiguration
{
    public string SigningKey { get; set; } = String.Empty;
    public string Issuer { get; set; } = "AppTemplate";
    public string Audience { get; set; } = "AppTemplate";
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
