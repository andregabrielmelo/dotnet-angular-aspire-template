using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Users.ForgotPassword;

/// <summary>
/// Starts the identity provider's own password-reset flow. Credentials never pass through this
/// application - the provider (Keycloak) emails the user a link to its hosted "set a new
/// password" page. Implemented in Infrastructure.
/// </summary>
public interface IPasswordResetService
{
    /// <returns>
    /// <see cref="Result.Success()"/> once the email is queued, <see cref="ResultStatus.NotFound"/>
    /// when no account uses <paramref name="email"/>, or an error when the provider fails.
    /// </returns>
    Task<Result> SendPasswordResetEmailAsync(
        EmailAddress email,
        CancellationToken cancellationToken
    );
}
