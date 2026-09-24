using System.Net.Http.Json;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Infrastructure.Data;
using AppTemplate.Infrastructure.Jobs;
using AppTemplate.Web.Features.UserFeatures;
using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppTemplate.FunctionalTests.Jobs;

public class BackgroundJobsTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    private JobStorage Storage => factory.Services.GetRequiredService<JobStorage>();

    private async Task<CurrentUserResponse> ProvisionAsync(string subject) =>
        (
            await factory
                .CreateAuthenticatedClient(subject)
                .GetFromJsonAsync<CurrentUserResponse>("/users/me")
        )!;

    private async Task RunWelcomeEmailJobAsync(int userId)
    {
        using var scope = factory.Services.CreateScope();
        await scope
            .ServiceProvider.GetRequiredService<WelcomeEmailJob>()
            .RunAsync(userId, CancellationToken.None);
    }

    [Fact]
    public void Startup_RegistersTheRecurringProfileSync()
    {
        using var connection = Storage.GetConnection();

        var job = Assert.Single(
            connection.GetRecurringJobs(),
            j => j.Id == SyncUserProfilesJob.RecurringJobId
        );

        Assert.Equal("0 * * * *", job.Cron);
        Assert.Equal(JobQueues.Default, job.Queue);
        Assert.Equal(nameof(SyncUserProfilesJob.RunAsync), job.Job.Method.Name);
    }

    [Fact]
    public async Task ProvisioningANewUser_EnqueuesAWelcomeEmail()
    {
        var me = await ProvisionAsync($"sub-{Guid.NewGuid():N}");

        var enqueued = Storage.GetMonitoringApi().EnqueuedJobs(JobQueues.Emails, 0, 1000);

        Assert.Contains(
            enqueued,
            job =>
                job.Value.Job.Type == typeof(WelcomeEmailJob) && (int)job.Value.Job.Args[0] == me.Id
        );
        // Enqueued, not sent: the request didn't wait on SMTP.
        Assert.DoesNotContain(factory.EmailSender.Sent, e => e.To == me.Email);
    }

    [Fact]
    public async Task WelcomeEmailJob_SendsOnceEvenWhenRunAgain()
    {
        var me = await ProvisionAsync($"sub-{Guid.NewGuid():N}");

        await RunWelcomeEmailJobAsync(me.Id);
        await RunWelcomeEmailJobAsync(me.Id); // a retry or duplicate enqueue

        var email = Assert.Single(factory.EmailSender.Sent, e => e.To == me.Email);
        Assert.Contains(me.Name, email.Body);

        using var scope = factory.Services.CreateScope();
        var user = await scope
            .ServiceProvider.GetRequiredService<ApplicationDatabaseContext>()
            .Users.FindAsync(UserId.From(me.Id));
        Assert.NotNull(user!.WelcomeEmailSentAtUtc);
    }

    [Fact]
    public async Task WelcomeEmailJob_ForADeletedUser_CompletesWithoutRetrying()
    {
        // Throwing would make Hangfire retry; a missing user should just end the job.
        await RunWelcomeEmailJobAsync(int.MaxValue);
    }
}
