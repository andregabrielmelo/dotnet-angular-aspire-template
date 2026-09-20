namespace AppTemplate.UseCases.Auth;

/// <summary>
/// Raised when a single-use auth token (email confirmation, password reset) is missing,
/// already consumed, or expired.
/// </summary>
public sealed class InvalidTokenException(
    string message = "This confirmation link is invalid or has expired."
) : Exception(message);
