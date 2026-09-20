using System.Security.Cryptography;
using System.Text;

namespace AppTemplate.UseCases.Auth;

/// <summary>Raw tokens (email confirmation, password reset, refresh) are emailed/handed to the
/// client and never persisted - only their hash is stored, mirroring the password-hashing
/// precedent elsewhere in this codebase.</summary>
internal static class TokenHasher
{
    public static string GenerateRawToken() => RandomNumberGenerator.GetHexString(64);

    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
