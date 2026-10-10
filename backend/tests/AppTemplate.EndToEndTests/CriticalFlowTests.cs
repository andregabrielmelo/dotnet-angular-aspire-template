using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace AppTemplate.EndToEndTests;

/// <summary>
/// The few browser flows that prove the pieces work together: Angular through the backend for
/// frontend, the OpenID Connect round trip to Keycloak, the session cookie, the proxied API
/// call with its access token, and Postgres behind it. Each test registers its own user, so the
/// tests don't depend on each other or on seeded accounts.
/// </summary>
[Collection(FullStackCollection.Name)]
[Trait(TestCategories.Name, TestCategories.RequiresFullStack)]
public class CriticalFlowTests(FullStackFixture fixture)
{
    private sealed record Account(string Username, string Password, string FullName);

    [Fact]
    public Task SignIn_ThroughKeycloak_LandsOnHomeWithTheUsersName() =>
        InBrowserAsync(
            nameof(SignIn_ThroughKeycloak_LandsOnHomeWithTheUsersName),
            async page =>
            {
                var account = await RegisterAsync(page);
                await SignOutAsync(page);

                await page.GotoAsync("/auth");
                await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
                await Field(page, "Username or email").FillAsync(account.Username);
                await Field(page, "Password").FillAsync(account.Password);
                await page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();

                await Expect(page).ToHaveURLAsync($"{FullStackFixture.BaseUrl}/home");
                await Expect(page.GetByRole(AriaRole.Heading, new() { Level = 1 }))
                    .ToHaveTextAsync($"Welcome, {account.FullName}!");
            }
        );

    [Fact]
    public Task Profile_UpdatedName_SurvivesAReload() =>
        InBrowserAsync(
            nameof(Profile_UpdatedName_SurvivesAReload),
            async page =>
            {
                await RegisterAsync(page);
                var newName = $"Renamed {Guid.NewGuid():N}"[..20];

                await page.GetByRole(AriaRole.Link, new() { Name = "Edit profile" }).ClickAsync();
                var name = Field(page, "Name");
                await Expect(name).Not.ToHaveValueAsync(string.Empty);
                await name.FillAsync(newName);
                await page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
                await Expect(page.GetByRole(AriaRole.Status)).ToHaveTextAsync("Profile saved.");

                await page.ReloadAsync();

                await Expect(Field(page, "Name")).ToHaveValueAsync(newName);
            }
        );

    [Fact]
    public Task SignOut_ThenAProtectedRoute_SendsYouToSignIn() =>
        InBrowserAsync(
            nameof(SignOut_ThenAProtectedRoute_SendsYouToSignIn),
            async page =>
            {
                await RegisterAsync(page);

                await SignOutAsync(page);
                await page.GotoAsync("/profile");

                await Expect(page).ToHaveURLAsync($"{FullStackFixture.BaseUrl}/auth");
                await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }))
                    .ToBeVisibleAsync();
            }
        );

    /// <summary>
    /// A form field by its visible label. Keycloak marks required fields with an asterisk
    /// ("Username *"), and Playwright matches a label pattern against the raw text, line
    /// breaks included, so the label must match exactly apart from those.
    /// </summary>
    private static ILocator Field(IPage page, string label) =>
        page.GetByLabel(new Regex($@"^\s*{Regex.Escape(label)}\s*\*?\s*$"));

    /// <summary>Creates a new Keycloak account through the app and lands signed in on /home.</summary>
    private static async Task<Account> RegisterAsync(IPage page)
    {
        var id = Guid.NewGuid().ToString("N")[..10];
        var account = new Account($"e2e-{id}", $"Pw-{id}-{id}", $"Ada {id}");

        await page.GotoAsync("/");
        await Expect(page).ToHaveURLAsync($"{FullStackFixture.BaseUrl}/auth");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create account" }).ClickAsync();

        await Field(page, "Username").FillAsync(account.Username);
        await Field(page, "Email").FillAsync($"{account.Username}@example.com");
        await Field(page, "First name").FillAsync("Ada");
        await Field(page, "Last name").FillAsync(id);
        await Field(page, "Password").FillAsync(account.Password);
        await Field(page, "Confirm password").FillAsync(account.Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        await Expect(page).ToHaveURLAsync($"{FullStackFixture.BaseUrl}/home");
        await Expect(page.GetByText("You're logged in.")).ToBeVisibleAsync();
        return account;
    }

    private static async Task SignOutAsync(IPage page)
    {
        await page.GotoAsync("/home");
        await page.GetByRole(AriaRole.Button, new() { Name = "Log out" }).ClickAsync();
        await Expect(page).ToHaveURLAsync($"{FullStackFixture.BaseUrl}/auth");
    }

    /// <summary>Runs a flow in a fresh browser profile and saves a Playwright trace if it fails.</summary>
    private async Task InBrowserAsync(string testName, Func<IPage, Task> flow)
    {
        await using var context = await fixture.NewContextAsync();
        var page = await context.NewPageAsync();
        try
        {
            await flow(page);
            await context.Tracing.StopAsync();
        }
        catch
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "playwright-traces");
            Directory.CreateDirectory(directory);
            await context.Tracing.StopAsync(
                new TracingStopOptions { Path = Path.Combine(directory, $"{testName}.zip") }
            );
            throw;
        }
    }
}
