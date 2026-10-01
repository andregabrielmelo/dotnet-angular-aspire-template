using System.Net.Http.Json;
using AppTemplate.Web.Features.UserFeatures;

namespace AppTemplate.FunctionalTests.UserFeatures;

public static class CurrentUserClientExtensions
{
    /// <summary>POST /users/me - creates the caller's profile (or returns the existing one).</summary>
    public static async Task<CurrentUserResponse> ProvisionMeAsync(this HttpClient client)
    {
        var response = await client.PostAsync("/users/me", content: null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CurrentUserResponse>())!;
    }
}
