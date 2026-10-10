using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.FunctionalTests.Jobs;
using AppTemplate.Infrastructure.Auditing;
using AppTemplate.Infrastructure.Data;
using AppTemplate.Infrastructure.Jobs;
using AppTemplate.Infrastructure.Jobs.RecurringJobs;
using AppTemplate.UseCases.Auditing;
using AppTemplate.UseCases.Authorization;
using AppTemplate.Web.Features.UserFeatures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppTemplate.FunctionalTests.Auditing;

/// <summary>
/// The audit log (ADR 017): what is recorded for an IAuditable entity (allowlisted
/// properties only, personal data masked), who it's attributed to, explicit security events,
/// and the bounded query endpoint.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class AuditingTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    private static string NewSubject() => $"sub-{Guid.NewGuid():N}";

    private async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    private Task<List<AuditEntry>> EntriesForAsync(string entityType, string entityKey) =>
        InScopeAsync(services =>
            services
                .GetRequiredService<ApplicationDatabaseContext>()
                .AuditEntries.AsNoTracking()
                .Where(entry => entry.EntityType == entityType && entry.EntityKey == entityKey)
                .OrderBy(entry => entry.OccurredAtUtc)
                .ToListAsync()
        );

    private static Dictionary<string, AuditValueChange> ChangesOf(AuditEntry entry) =>
        JsonSerializer.Deserialize<Dictionary<string, AuditValueChange>>(
            entry.Changes!,
            AuditEntry.ChangesJsonOptions
        )!;

    private async Task<CurrentUserResponse> ProvisionAsync(string subject) =>
        (
            await factory
                .CreateAuthenticatedClient(subject)
                .GetFromJsonAsync<CurrentUserResponse>("/v1/users/me")
        )!;

    [Fact]
    public async Task Insert_RecordsAllowlistedValuesWithPersonalDataMasked()
    {
        var subject = NewSubject();
        var me = await ProvisionAsync(subject);

        var created = Assert.Single(
            await EntriesForAsync(nameof(User), me.Id.ToString()),
            entry => entry.Actor == $"user:{subject}"
        );

        Assert.Equal(AuditActions.Created, created.Action);
        Assert.Equal($"user:{subject}", created.Actor);
        Assert.Equal("succeeded", created.Outcome);
        Assert.False(string.IsNullOrEmpty(created.TraceId));
        var changes = ChangesOf(created);
        Assert.Equal(new AuditValueChange(null, me.Name), changes["name"]);
        Assert.Equal(new AuditValueChange(null, AuditEntry.Masked), changes["email"]);
        // Not allowlisted, so never recorded, whatever it contains.
        Assert.DoesNotContain("externalId", changes.Keys);
        Assert.DoesNotContain("createdAtUtc", changes.Keys);
        Assert.DoesNotContain(me.Email, created.Changes!);
    }

    [Fact]
    public async Task Update_RecordsOnlyWhatChanged_AndMasksThePhoneNumber()
    {
        var me = await ProvisionAsync(NewSubject());
        var admin = NewSubject();

        var response = await factory
            .CreateAuthenticatedClient(admin, Permission.UsersWrite)
            .PutAsJsonAsync(
                $"/v1/users/{me.Id}",
                new
                {
                    id = me.Id,
                    name = "Renamed For Audit",
                    phoneNumber = "11999998888",
                    phoneCountryCode = "+55",
                }
            );
        response.EnsureSuccessStatusCode();

        var updated = Assert.Single(
            await EntriesForAsync(nameof(User), me.Id.ToString()),
            entry => entry.Action == AuditActions.Updated && entry.Actor == $"user:{admin}"
        );
        var changes = ChangesOf(updated);
        Assert.Equal(new AuditValueChange(me.Name, "Renamed For Audit"), changes["name"]);
        Assert.Equal(
            new AuditValueChange(AuditEntry.Masked, AuditEntry.Masked),
            changes["phoneNumber"]
        );
        Assert.DoesNotContain("email", changes.Keys); // unchanged
        Assert.DoesNotContain("11999998888", updated.Changes!);
    }

    [Fact]
    public async Task Delete_RecordsTheOldValues()
    {
        var me = await ProvisionAsync(NewSubject());
        var admin = NewSubject();

        var response = await factory
            .CreateAuthenticatedClient(admin, Permission.UsersDelete)
            .DeleteAsync($"/v1/users/{me.Id}");
        response.EnsureSuccessStatusCode();

        var deleted = Assert.Single(
            await EntriesForAsync(nameof(User), me.Id.ToString()),
            entry => entry.Action == AuditActions.Deleted && entry.Actor == $"user:{admin}"
        );
        Assert.Equal(new AuditValueChange(me.Name, null), ChangesOf(deleted)["name"]);
    }

    [Fact]
    public async Task ChangeToUnauditedPropertiesOnly_RecordsNothing()
    {
        var me = await ProvisionAsync(NewSubject());
        var before = (await EntriesForAsync(nameof(User), me.Id.ToString())).Count;

        await InScopeAsync(async services =>
        {
            var context = services.GetRequiredService<ApplicationDatabaseContext>();
            var user = await context.Users.SingleAsync(u => u.Id == UserId.From(me.Id));
            user.MarkWelcomeEmailSent(DateTimeOffset.UtcNow); // not allowlisted
            return await context.SaveChangesAsync();
        });

        Assert.Equal(before, (await EntriesForAsync(nameof(User), me.Id.ToString())).Count);
    }

    [Fact]
    public async Task RecurringJobChanges_AreAttributedToTheJob()
    {
        var me = await ProvisionAsync(NewSubject());
        factory.TestRecurringJob.Action = async services =>
        {
            var context = services.GetRequiredService<ApplicationDatabaseContext>();
            var user = await context.Users.SingleAsync(u => u.Id == UserId.From(me.Id));
            user.UpdateName(UserName.From("Renamed By Job"));
            await context.SaveChangesAsync();
        };
        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await scope
                .ServiceProvider.GetRequiredService<RecurringJobRunner>()
                .ExecuteAsync(TestRecurringJobDefinition.Id, CancellationToken.None);
        }
        finally
        {
            factory.TestRecurringJob.Action = null;
        }

        var updated = Assert.Single(
            await EntriesForAsync(nameof(User), me.Id.ToString()),
            entry =>
                entry.Action == AuditActions.Updated
                && entry.Actor == $"system:{TestRecurringJobDefinition.Id}"
        );
        Assert.Equal("Renamed By Job", ChangesOf(updated)["name"].New);
    }

    [Fact]
    public async Task ChangesWithoutAUserOrJob_AreAttributedToAnonymous()
    {
        var externalId = NewSubject();
        var id = await InScopeAsync(async services =>
        {
            var context = services.GetRequiredService<ApplicationDatabaseContext>();
            var user = User.Create(
                externalId,
                UserName.From("Ada"),
                new EmailAddress($"{externalId}@example.com")
            );
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user.Id.Value;
        });

        Assert.Equal(
            AuditActorContext.Anonymous,
            Assert.Single(await EntriesForAsync(nameof(User), id.ToString())).Actor
        );
    }

    [Fact]
    public async Task JobMutation_IsRecordedAsAnExplicitEvent()
    {
        var subject = NewSubject();

        var response = await factory
            .CreateAuthenticatedClient(subject, Permission.JobsManage)
            .PostAsync($"/v1/admin/jobs/{TestRecurringJobDefinition.Id}/trigger", null);
        response.EnsureSuccessStatusCode();

        var entry = Assert.Single(
            await EntriesForAsync("job", TestRecurringJobDefinition.Id),
            e => e.Action == AuditActions.JobTriggered && e.Actor == $"user:{subject}"
        );
        Assert.Equal("succeeded", entry.Outcome);
    }

    [Fact]
    public async Task DeniedAdminRequest_IsRecorded()
    {
        var subject = NewSubject();

        var response = await factory
            .CreateAuthenticatedClient(subject) // no jobs:manage
            .PostAsync($"/v1/admin/jobs/{TestRecurringJobDefinition.Id}/pause", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var denied = Assert.Single(
            await InScopeAsync(services =>
                services
                    .GetRequiredService<ApplicationDatabaseContext>()
                    .AuditEntries.AsNoTracking()
                    .Where(entry => entry.Actor == $"user:{subject}")
                    .ToListAsync()
            )
        );
        Assert.Equal(AuditActions.AuthorizationDenied, denied.Action);
        Assert.Equal("denied", denied.Outcome);
        Assert.Equal("POST /v1/admin/jobs/{JobId}/pause", denied.EntityKey);
    }

    [Fact]
    public async Task Query_ReturnsEntriesForAnEntityToAnAuditReader()
    {
        var subject = NewSubject();
        var me = await ProvisionAsync(subject);

        // Row ids are reused after a delete, so narrow by actor too.
        var page = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.AuditRead)
            .GetFromJsonAsync<JsonElement>(
                $"/v1/admin/audit?entityType=User&entityKey={me.Id}&actor=user:{subject}&per_page=10"
            );

        var item = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal("created", item.GetProperty("action").GetString());
        Assert.Equal(
            AuditEntry.Masked,
            item.GetProperty("changes").GetProperty("email").GetProperty("new").GetString()
        );
    }

    [Fact]
    public async Task Query_WithoutAuditRead_Is403()
    {
        var response = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersRead)
            .GetAsync("/v1/admin/audit");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("per_page=101")]
    [InlineData("per_page=0")]
    [InlineData("page=0")]
    [InlineData("from=2026-10-02T00:00:00Z&to=2026-10-01T00:00:00Z")]
    public async Task Query_OutsideTheBounds_Is400(string query)
    {
        var response = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.AuditRead)
            .GetAsync($"/v1/admin/audit?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Query_WithoutARange_CoversTheLastSevenDays()
    {
        var key = Guid.NewGuid().ToString("N");
        await InScopeAsync(async services =>
        {
            var context = services.GetRequiredService<ApplicationDatabaseContext>();
            foreach (var daysAgo in new[] { 1, 8 })
            {
                context.AuditEntries.Add(
                    new AuditEntry
                    {
                        Id = Guid.CreateVersion7(),
                        OccurredAtUtc = DateTimeOffset.UtcNow.AddDays(-daysAgo),
                        Actor = "anonymous",
                        Action = "test.range",
                        EntityType = "test",
                        EntityKey = key,
                        Outcome = "succeeded",
                    }
                );
            }
            return await context.SaveChangesAsync();
        });

        var page = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.AuditRead)
            .GetFromJsonAsync<JsonElement>($"/v1/admin/audit?entityType=test&entityKey={key}");

        Assert.Single(page.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task RetentionJob_DeletesOnlyEntriesOlderThanTheRetention()
    {
        var oldKey = Guid.NewGuid().ToString("N");
        var recentKey = Guid.NewGuid().ToString("N");
        await InScopeAsync(async services =>
        {
            var context = services.GetRequiredService<ApplicationDatabaseContext>();
            foreach (var (key, daysAgo) in new[] { (oldKey, 400), (recentKey, 300) })
            {
                context.AuditEntries.Add(
                    new AuditEntry
                    {
                        Id = Guid.CreateVersion7(),
                        OccurredAtUtc = DateTimeOffset.UtcNow.AddDays(-daysAgo),
                        Actor = "anonymous",
                        Action = "test.retention",
                        EntityType = "test",
                        EntityKey = key,
                        Outcome = "succeeded",
                    }
                );
            }
            return await context.SaveChangesAsync();
        });

        await InScopeAsync(async services =>
        {
            await services
                .GetRequiredService<AuditRetentionJob>()
                .ExecuteAsync(CancellationToken.None);
            return 0;
        });

        Assert.Empty(await EntriesForAsync("test", oldKey)); // default retention: 365 days
        Assert.Single(await EntriesForAsync("test", recentKey));
    }
}

/// <summary>An audit write that fails takes the change down with it: no unaudited change.</summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class FailingAuditWriteTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    [Fact]
    public async Task UpdateWhoseAuditRowIsRejected_FailsAndChangesNothing()
    {
        var me = await factory
            .CreateAuthenticatedClient($"sub-{Guid.NewGuid():N}")
            .GetFromJsonAsync<CurrentUserResponse>("/v1/users/me");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            // From now on the database refuses every audit row (this host's database only).
            await scope
                .ServiceProvider.GetRequiredService<ApplicationDatabaseContext>()
                .Database.ExecuteSqlRawAsync(
                    "ALTER TABLE audit_entries ADD CONSTRAINT reject_all CHECK (false) NOT VALID"
                );
        }

        var response = await factory
            .CreateAuthenticatedClient($"sub-{Guid.NewGuid():N}", Permission.UsersWrite)
            .PutAsJsonAsync($"/v1/users/{me!.Id}", new { id = me.Id, name = "Never Saved" });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var check = factory.Services.CreateAsyncScope();
        var user = await check
            .ServiceProvider.GetRequiredService<ApplicationDatabaseContext>()
            .Users.AsNoTracking()
            .SingleAsync(u => u.Id == UserId.From(me.Id));
        Assert.Equal(me.Name, user.Name.Value);
    }
}

/// <summary>Auditing is opt-in: with Audit:Enabled=false the app works and records nothing.</summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class AuditingDisabledTests(AuditingDisabledTests.NoAuditFactory factory)
    : IClassFixture<AuditingDisabledTests.NoAuditFactory>
{
    [Fact]
    public async Task WithAuditingOff_ChangesAndAdminActionsStillWork_AndNothingIsRecorded()
    {
        var me = await factory
            .CreateAuthenticatedClient($"sub-{Guid.NewGuid():N}")
            .GetFromJsonAsync<CurrentUserResponse>("/v1/users/me");
        var update = await factory
            .CreateAuthenticatedClient($"sub-{Guid.NewGuid():N}", Permission.UsersWrite)
            .PutAsJsonAsync($"/v1/users/{me!.Id}", new { id = me.Id, name = "Renamed" });
        var trigger = await factory
            .CreateAuthenticatedClient($"sub-{Guid.NewGuid():N}", Permission.JobsManage)
            .PostAsync($"/v1/admin/jobs/{TestRecurringJobDefinition.Id}/trigger", null);

        update.EnsureSuccessStatusCode();
        trigger.EnsureSuccessStatusCode();
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(
            await scope
                .ServiceProvider.GetRequiredService<ApplicationDatabaseContext>()
                .AuditEntries.AnyAsync()
        );
    }

    public sealed class NoAuditFactory : AppTemplateWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Audit:Enabled", "false");
        }
    }
}
