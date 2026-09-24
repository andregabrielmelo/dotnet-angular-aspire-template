using System.Net;
using System.Text;
using System.Text.Json;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.UseCases.Users.SyncProfiles;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace AppTemplate.FunctionalTests.Identity;

public class KeycloakUserDirectoryTests
{
    private readonly List<Uri> _requests = [];

    private KeycloakUserDirectory CreateDirectory(int totalUsers)
    {
        var handler = new StubHandler(request =>
        {
            _requests.Add(request.RequestUri!);
            var query = QueryHelpers.ParseQuery(request.RequestUri!.Query);
            var first = int.Parse(query["first"]!);
            var max = int.Parse(query["max"]!);
            var page = Enumerable
                .Range(first, Math.Max(0, Math.Min(max, totalUsers - first)))
                .Select(i =>
                    i == 0
                        ? new
                        {
                            id = "sa",
                            username = "service-account-apptemplate-user-admin",
                            email = (string?)null,
                            firstName = (string?)null,
                            lastName = (string?)null,
                        }
                        : new
                        {
                            id = $"id-{i}",
                            username = $"user{i}",
                            email = (string?)$"user{i}@example.com",
                            firstName = (string?)(i % 2 == 0 ? "Ada" : null),
                            lastName = (string?)(i % 2 == 0 ? "Lovelace" : null),
                        }
                );
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(page),
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        });

        return new KeycloakUserDirectory(
            new HttpClient(handler) { BaseAddress = new Uri("http://keycloak.test/") },
            Microsoft.Extensions.Options.Options.Create(
                new KeycloakAdminOptions { ClientSecret = "secret" }
            )
        );
    }

    private static async Task<List<IdentityProviderUser>> ReadAll(KeycloakUserDirectory directory)
    {
        var users = new List<IdentityProviderUser>();
        await foreach (var user in directory.ListUsersAsync(CancellationToken.None))
        {
            users.Add(user);
        }
        return users;
    }

    [Fact]
    public async Task ListUsers_PagesUntilAShortPage_AndSkipsServiceAccounts()
    {
        var users = await ReadAll(CreateDirectory(totalUsers: KeycloakUserDirectory.PageSize + 20));

        Assert.Equal(2, _requests.Count);
        Assert.Contains("first=100&max=100", _requests[1].Query);
        Assert.Equal(KeycloakUserDirectory.PageSize + 19, users.Count);
        Assert.DoesNotContain(users, u => u.Id == "sa");
    }

    [Fact]
    public async Task ListUsers_UsesTheFullNameOrFallsBackToTheUsername()
    {
        var users = await ReadAll(CreateDirectory(totalUsers: 3));

        Assert.Equal("user1", users.Single(u => u.Id == "id-1").Name);
        Assert.Equal("Ada Lovelace", users.Single(u => u.Id == "id-2").Name);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(respond(request));
    }
}
