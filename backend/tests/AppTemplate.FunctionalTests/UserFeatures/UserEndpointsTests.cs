using System.Net;
using System.Net.Http.Json;
using AppTemplate.UseCases;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Users;
using AppTemplate.UseCases.Users.List;
using AppTemplate.Web.Features.UserFeatures;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AppTemplate.FunctionalTests.UserFeatures;

public class UserEndpointsTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    private static string NewSubject() => $"sub-{Guid.NewGuid():N}";

    private async Task<CurrentUserResponse> ProvisionAsync(
        string subject,
        params string[] permissions
    )
    {
        var me = await factory
            .CreateAuthenticatedClient(subject, permissions)
            .GetFromJsonAsync<CurrentUserResponse>("/users/me");
        return me!;
    }

    // --- Authentication -------------------------------------------------------------------

    [Fact]
    public async Task List_WithoutAuthentication_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- /users/me (any authenticated user) -------------------------------------------------

    [Fact]
    public async Task Me_OnFirstCall_ProvisionsTheUserFromTokenClaims()
    {
        var subject = NewSubject();

        var me = await ProvisionAsync(subject);

        Assert.Equal($"Test {subject}", me.Name);
        Assert.Equal($"{subject}@example.com", me.Email);
        Assert.Empty(me.Permissions);
    }

    [Fact]
    public async Task Me_CalledTwice_ReturnsTheSameUser()
    {
        var client = factory.CreateAuthenticatedClient(NewSubject());

        var first = await client.GetFromJsonAsync<CurrentUserResponse>("/users/me");
        var second = await client.GetFromJsonAsync<CurrentUserResponse>("/users/me");

        Assert.Equal(first!.Id, second!.Id);
    }

    [Fact]
    public async Task Me_ReturnsOnlyKnownPermissions()
    {
        var me = await ProvisionAsync(NewSubject(), Permission.UsersRead, "not-a-permission");

        Assert.Equal([Permission.UsersRead], me.Permissions);
    }

    // --- Permission policies ----------------------------------------------------------------

    [Fact]
    public async Task List_WithoutUsersRead_IsForbidden()
    {
        var response = await factory.CreateAuthenticatedClient(NewSubject()).GetAsync("/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_WithUsersRead_ReturnsUsers()
    {
        // ListUsersQueryService uses raw SQL, which EF Core's InMemory provider can't run
        // (see ADR 006) - stub it, since this test is about authorization.
        var client = factory
            .WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IListUsersQueryService>();
                    services.AddSingleton<IListUsersQueryService, EmptyListUsersQueryService>();
                })
            )
            .CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, NewSubject());
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, Permission.UsersRead);

        var response = await client.GetAsync("/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class EmptyListUsersQueryService : IListUsersQueryService
    {
        public Task<PagedResult<UserDto>> ListAsync(int page, int perPage) =>
            Task.FromResult(new PagedResult<UserDto>([], page, perPage, 0, 0));
    }

    [Fact]
    public async Task GetById_WithoutUsersRead_IsForbidden()
    {
        var me = await ProvisionAsync(NewSubject());

        var response = await factory
            .CreateAuthenticatedClient(NewSubject())
            .GetAsync($"/users/{me.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithUsersRead_ReturnsTheUser()
    {
        var me = await ProvisionAsync(NewSubject());

        var response = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersRead)
            .GetAsync($"/users/{me.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithUnknownId_ReturnsNotFound()
    {
        var response = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersRead)
            .GetAsync("/users/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutUsersDelete_IsForbiddenAndKeepsTheUser()
    {
        var target = await ProvisionAsync(NewSubject());

        var response = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersRead, Permission.UsersWrite)
            .DeleteAsync($"/users/{target.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var stillThere = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersRead)
            .GetAsync($"/users/{target.Id}");
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task Delete_WithUsersDelete_RemovesTheUser()
    {
        var target = await ProvisionAsync(NewSubject());

        var response = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersDelete)
            .DeleteAsync($"/users/{target.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // --- Resource-based: update own profile vs. anyone's -------------------------------------

    [Fact]
    public async Task Update_OwnProfile_IsAllowedWithoutPermissions()
    {
        var subject = NewSubject();
        var me = await ProvisionAsync(subject);

        var response = await factory
            .CreateAuthenticatedClient(subject)
            .PutAsJsonAsync($"/users/{me.Id}", new { id = me.Id, name = "Renamed Myself" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_SomeoneElse_WithoutUsersWrite_IsForbidden()
    {
        var target = await ProvisionAsync(NewSubject());

        var response = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersRead)
            .PutAsJsonAsync($"/users/{target.Id}", new { id = target.Id, name = "Hijacked" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_SomeoneElse_WithUsersWrite_IsAllowed()
    {
        var target = await ProvisionAsync(NewSubject());

        var response = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersWrite)
            .PutAsJsonAsync(
                $"/users/{target.Id}",
                new { id = target.Id, name = "Renamed By Admin" }
            );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
