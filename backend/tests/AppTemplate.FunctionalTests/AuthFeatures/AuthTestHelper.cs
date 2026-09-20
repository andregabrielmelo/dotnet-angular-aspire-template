using System.Net.Http.Json;
using System.Text.RegularExpressions;
using AppTemplate.FunctionalTests.TestDoubles;
using AppTemplate.Web.Features.AuthFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace AppTemplate.FunctionalTests.AuthFeatures;

/// <summary>
/// Drives the real register -> confirm-email -> login HTTP flow, pulling the confirmation
/// token out of the RecordingEmailSender-captured email instead of touching the database
/// directly, so tests that just need an authenticated user exercise the same path a real one
/// would.
/// </summary>
public static class AuthTestHelper
{
    private static readonly Regex ConfirmationTokenPattern = new(@"token=([0-9A-Fa-f]+)");

    public static async Task<string> RegisterConfirmAndLoginAsync(
        AppTemplateWebApplicationFactory factory,
        HttpClient client,
        string? email = null,
        string password = "Passw0rd!"
    )
    {
        email ??= $"user-{Guid.NewGuid():N}@example.com";

        var registerResponse = await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest
            {
                Name = "Test User",
                Email = email,
                Password = password,
            }
        );
        registerResponse.EnsureSuccessStatusCode();

        var emailSender = factory.Services.GetRequiredService<RecordingEmailSender>();
        var confirmationEmail = emailSender.SentEmails.Last(e => e.To == email);
        var token = ConfirmationTokenPattern.Match(confirmationEmail.Body).Groups[1].Value;

        var confirmResponse = await client.PostAsJsonAsync(
            "/auth/confirm-email",
            new ConfirmEmailRequest { Token = token }
        );
        confirmResponse.EnsureSuccessStatusCode();

        var loginResponse = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest { Email = email, Password = password }
        );
        loginResponse.EnsureSuccessStatusCode();

        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthTokenResponse>();
        return authResult!.AccessToken;
    }
}
