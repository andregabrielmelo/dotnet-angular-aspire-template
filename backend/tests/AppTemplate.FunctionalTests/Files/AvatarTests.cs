using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Infrastructure.Data;
using AppTemplate.Infrastructure.Outbox;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Files;
using AppTemplate.Web.Features.UserFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppTemplate.FunctionalTests.Files;

/// <summary>
/// File storage against a real Garage, and the avatar slice end to end: owner-only uploads,
/// what's refused, the stored result, conditional GETs, and old objects deleted via the outbox.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class AvatarTests(GarageFactory factory) : IClassFixture<GarageFactory>
{
    private static string NewSubject() => $"sub-{Guid.NewGuid():N}";

    private IFileStorage Storage => factory.Services.GetRequiredService<IFileStorage>();

    private async Task<(HttpClient Client, CurrentUserResponse Me)> SignInAsync(
        params string[] permissions
    )
    {
        var client = factory.CreateAuthenticatedClient(NewSubject(), permissions);
        var me = (await client.GetFromJsonAsync<CurrentUserResponse>("/v1/users/me"))!;
        return (client, me);
    }

    private static Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        int userId,
        byte[] bytes,
        string contentType = "image/png",
        string fileName = "avatar.png"
    )
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { file, "file", fileName } };
        return client.PutAsync($"/v1/users/{userId}/avatar", form);
    }

    private Task<string?> AvatarKeyAsync(int userId) =>
        WithContextAsync(context =>
            context
                .Users.AsNoTracking()
                .Where(user => user.Id == UserId.From(userId))
                .Select(user => user.AvatarKey)
                .SingleOrDefaultAsync()
        );

    private async Task<T> WithContextAsync<T>(Func<ApplicationDatabaseContext, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDatabaseContext>());
    }

    private Task DeliverOutboxAsync() =>
        factory
            .Services.GetRequiredService<OutboxProcessor>()
            .ProcessPendingAsync(CancellationToken.None);

    [Fact]
    public async Task Storage_RoundTripsAnObject()
    {
        var key = $"tests/{Guid.NewGuid():N}";
        var content = Encoding.UTF8.GetBytes("hello garage");

        await Storage.PutAsync(key, content, "text/plain", CancellationToken.None);
        await using (var stored = await Storage.GetAsync(key, CancellationToken.None))
        {
            Assert.NotNull(stored);
            Assert.Equal("text/plain", stored.ContentType);
            using var reader = new StreamReader(stored.Content);
            Assert.Equal("hello garage", await reader.ReadToEndAsync());
        }
        await Storage.DeleteAsync(key, CancellationToken.None);

        Assert.Null(await Storage.GetAsync(key, CancellationToken.None));
        await Storage.DeleteAsync(key, CancellationToken.None); // deleting twice is fine (retries)
    }

    [Fact]
    public async Task Owner_UploadsAndReadsBackAReEncodedAvatar()
    {
        var (client, me) = await SignInAsync();

        var upload = await UploadAsync(
            client,
            me.Id,
            TestImages.JpegWithExif(800, 600),
            "image/jpeg",
            "me.jpg"
        );
        var avatar = await client.GetAsync($"/v1/users/{me.Id}/avatar");

        Assert.Equal(HttpStatusCode.NoContent, upload.StatusCode);
        Assert.Equal(HttpStatusCode.OK, avatar.StatusCode);
        Assert.Equal("image/webp", avatar.Content.Headers.ContentType?.MediaType);
        Assert.True(avatar.Headers.CacheControl is { Private: true, NoCache: true });
        var bytes = await avatar.Content.ReadAsByteArrayAsync();
        Assert.Equal("WEBP", Encoding.ASCII.GetString(bytes, 8, 4));
        Assert.DoesNotContain(TestImages.ExifMarker, Encoding.ASCII.GetString(bytes));
        var key = await AvatarKeyAsync(me.Id);
        Assert.Matches("^avatars/[0-9a-f]{32}$", key); // opaque, nothing from the upload
        Assert.Equal($"\"{key}\"", avatar.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task MatchingIfNoneMatch_Is304()
    {
        var (client, me) = await SignInAsync();
        await UploadAsync(client, me.Id, TestImages.Png(64, 64));
        var first = await client.GetAsync($"/v1/users/{me.Id}/avatar");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/users/{me.Id}/avatar");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        var second = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
        Assert.Equal(first.Headers.ETag, second.Headers.ETag);
    }

    [Fact]
    public async Task NonOwner_CannotUploadOrDelete_EvenWithUsersWrite()
    {
        var (_, owner) = await SignInAsync();
        var (admin, _) = await SignInAsync(Permission.UsersWrite, Permission.UsersDelete);

        var upload = await UploadAsync(admin, owner.Id, TestImages.Png(64, 64));
        var delete = await admin.DeleteAsync($"/v1/users/{owner.Id}/avatar");

        Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Null(await AvatarKeyAsync(owner.Id));
    }

    [Fact]
    public async Task SomeoneElsesAvatar_NeedsUsersRead()
    {
        var (ownerClient, owner) = await SignInAsync();
        await UploadAsync(ownerClient, owner.Id, TestImages.Png(64, 64));
        var (stranger, _) = await SignInAsync();
        var (reader, _) = await SignInAsync(Permission.UsersRead);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await stranger.GetAsync($"/v1/users/{owner.Id}/avatar")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.OK,
            (await reader.GetAsync($"/v1/users/{owner.Id}/avatar")).StatusCode
        );
    }

    [Fact]
    public async Task SpoofedContentType_IsJudgedByTheBytes()
    {
        var (client, me) = await SignInAsync();

        var response = await UploadAsync(
            client,
            me.Id,
            Encoding.UTF8.GetBytes("<script>alert(1)</script>"),
            "image/png",
            "innocent.png"
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(
            "JPEG, PNG or WebP",
            problem.GetProperty("errors").GetProperty("file")[0].GetString()
        );
        Assert.Null(await AvatarKeyAsync(me.Id));
    }

    [Fact]
    public async Task OversizedAndBombImages_AreRefused()
    {
        var (client, me) = await SignInAsync();
        var tooBig = new byte[AvatarLimits.MaxBytes + 1];
        TestImages.Png(10, 10).CopyTo(tooBig, 0);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await UploadAsync(client, me.Id, tooBig)).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await UploadAsync(client, me.Id, TestImages.PngBomb(20_000, 20_000))).StatusCode
        );
        Assert.Null(await AvatarKeyAsync(me.Id));
    }

    [Fact]
    public async Task ReplacedAvatar_HasItsOldObjectDeletedThroughTheOutbox()
    {
        var (client, me) = await SignInAsync();
        await UploadAsync(client, me.Id, TestImages.Png(64, 64));
        var oldKey = (await AvatarKeyAsync(me.Id))!;

        await UploadAsync(client, me.Id, TestImages.Png(96, 96));
        Assert.NotNull(await Storage.GetAsync(oldKey, CancellationToken.None)); // until delivered
        await DeliverOutboxAsync();

        Assert.Null(await Storage.GetAsync(oldKey, CancellationToken.None));
        Assert.NotNull(
            await Storage.GetAsync((await AvatarKeyAsync(me.Id))!, CancellationToken.None)
        );
    }

    [Fact]
    public async Task DeletedUser_HasItsAvatarDeletedThroughTheOutbox()
    {
        var (client, me) = await SignInAsync();
        await UploadAsync(client, me.Id, TestImages.Png(64, 64));
        var key = (await AvatarKeyAsync(me.Id))!;
        var (admin, _) = await SignInAsync(Permission.UsersDelete);

        (await admin.DeleteAsync($"/v1/users/{me.Id}")).EnsureSuccessStatusCode();
        await DeliverOutboxAsync();

        Assert.Null(await Storage.GetAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task RemovedAvatar_Is404AndItsObjectGoes()
    {
        var (client, me) = await SignInAsync();
        await UploadAsync(client, me.Id, TestImages.Png(64, 64));
        var key = (await AvatarKeyAsync(me.Id))!;

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/v1/users/{me.Id}/avatar")).StatusCode
        );
        await DeliverOutboxAsync();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/v1/users/{me.Id}/avatar")).StatusCode
        );
        Assert.Null(await Storage.GetAsync(key, CancellationToken.None));
    }
}

/// <summary>Garage is optional: with it unreachable, only file endpoints fail (503), and readiness holds.</summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class StorageOutageTests(StorageOutageTests.UnreachableStorageFactory factory)
    : IClassFixture<StorageOutageTests.UnreachableStorageFactory>
{
    [Fact]
    public async Task WithStorageUnreachable_AvatarsAre503AndEverythingElseWorks()
    {
        var client = factory.CreateAuthenticatedClient($"sub-{Guid.NewGuid():N}");
        var me = (await client.GetFromJsonAsync<CurrentUserResponse>("/v1/users/me"))!;
        var file = new ByteArrayContent(TestImages.Png(64, 64));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        var upload = await client.PutAsync(
            $"/v1/users/{me.Id}/avatar",
            new MultipartFormDataContent { { file, "file", "a.png" } }
        );
        var health = await factory.CreateClient().GetAsync("/health");
        var dependencies = await factory.CreateClient().GetStringAsync("/health/dependencies");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, upload.StatusCode);
        Assert.Equal("application/problem+json", upload.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal("Degraded", dependencies);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/v1/users/me")).StatusCode);
    }

    public sealed class UnreachableStorageFactory : AppTemplateWebApplicationFactory
    {
        protected override void ConfigureWebHost(
            Microsoft.AspNetCore.Hosting.IWebHostBuilder builder
        )
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("FileStorage:ServiceUrl", "http://127.0.0.1:1"); // refuses connections
            builder.UseSetting("FileStorage:AccessKey", GarageFactory.AccessKey);
            builder.UseSetting("FileStorage:SecretKey", GarageFactory.SecretKey);
            builder.UseSetting("FileStorage:Timeout", "00:00:02");
        }
    }
}
