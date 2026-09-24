using System.Net.Http.Json;
using AppTemplate.UseCases.Users.ForgotPassword;
using Ardalis.Result;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Identity;

/// <summary>
/// Uses Keycloak's Admin REST API to send the "update password" action email. The HttpClient
/// is a typed client whose handler attaches a service-account access token (see
/// <see cref="IdentityServiceExtensions"/>).
/// </summary>
public sealed partial class KeycloakPasswordResetService(
    HttpClient httpClient,
    IOptions<KeycloakAdminOptions> options,
    ILogger<KeycloakPasswordResetService> logger
) : IPasswordResetService
{
    private const string UpdatePasswordAction = "UPDATE_PASSWORD";

    private readonly KeycloakAdminOptions _options = options.Value;

    public async Task<Result> SendPasswordResetEmailAsync(
        EmailAddress email,
        CancellationToken cancellationToken
    )
    {
        var realm = Uri.EscapeDataString(_options.Realm);

        try
        {
            var users = await httpClient.GetFromJsonAsync<KeycloakUser[]>(
                $"admin/realms/{realm}/users?email={Uri.EscapeDataString(email.Value)}&exact=true&briefRepresentation=true",
                cancellationToken
            );

            var userId = users?.FirstOrDefault()?.Id;
            if (userId is null)
            {
                LogNoAccount(logger);
                return Result.NotFound();
            }

            var query =
                $"client_id={Uri.EscapeDataString(_options.PasswordResetClientId)}"
                + $"&redirect_uri={Uri.EscapeDataString(_options.PasswordResetRedirectUri.ToString())}"
                + $"&lifespan={(int)_options.PasswordResetLinkLifespan.TotalSeconds}";

            using var response = await httpClient.PutAsJsonAsync(
                $"admin/realms/{realm}/users/{Uri.EscapeDataString(userId)}/execute-actions-email?{query}",
                new[] { UpdatePasswordAction },
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                LogKeycloakFailure(logger, (int)response.StatusCode, userId);
                return Result.Error("The password reset email could not be sent.");
            }

            LogResetEmailSent(logger, userId);
            return Result.Success();
        }
        catch (HttpRequestException exception)
        {
            LogKeycloakUnreachable(logger, exception);
            return Result.Unavailable("The identity provider is unavailable.");
        }
    }

    private sealed record KeycloakUser(string Id);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Password reset requested for an email with no account"
    )]
    private static partial void LogNoAccount(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Password reset email sent to user {UserId}"
    )]
    private static partial void LogResetEmailSent(ILogger logger, string userId);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Keycloak answered {StatusCode} when sending the password reset email to user {UserId}"
    )]
    private static partial void LogKeycloakFailure(ILogger logger, int statusCode, string userId);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Keycloak could not be reached for a password reset"
    )]
    private static partial void LogKeycloakUnreachable(ILogger logger, Exception exception);
}
