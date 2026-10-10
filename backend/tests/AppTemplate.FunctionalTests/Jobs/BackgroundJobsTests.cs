using System.Net.Http.Json;
using AppTemplate.Infrastructure.Jobs;
using AppTemplate.Infrastructure.Jobs.RecurringJobs;
using AppTemplate.Infrastructure.Outbox;
using AppTemplate.Web.Features.UserFeatures;
using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppTemplate.FunctionalTests.Jobs;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class BackgroundJobsTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    private JobStorage Storage => factory.Services.GetRequiredService<JobStorage>();

    private async Task<CurrentUserResponse> ProvisionAsync(string subject) =>
        (
            await factory
                .CreateAuthenticatedClient(subject)
                .GetFromJsonAsync<CurrentUserResponse>("/v1/users/me")
        )!;

    [Theory]
    [InlineData(SyncUserProfilesJob.Id, "0 * * * *")]
    [InlineData(TestRecurringJobDefinition.Id, "0 0 * * *")]
    [InlineData(OutboxSweepJob.Id, "* * * * *")]
    public void Startup_SchedulesEveryRecurringJobDefinitionThroughTheRunner(
        string jobId,
        string cron
    )
    {
        using var connection = Storage.GetConnection();

        var job = Assert.Single(connection.GetRecurringJobs(), j => j.Id == jobId);

        Assert.Equal(cron, job.Cron);
        Assert.Equal(JobQueues.Default, job.Queue);
        Assert.Equal("UTC", job.TimeZoneId);
        // Hangfire stores only the runner and the job id - never the definition itself.
        Assert.Equal(typeof(RecurringJobRunner), job.Job.Type);
        Assert.Equal(nameof(RecurringJobRunner.ExecuteAsync), job.Job.Method.Name);
        Assert.Equal(jobId, job.Job.Args[0]);
    }

    [Fact]
    public async Task ProvisioningANewUser_StartsTheOutboxRelayWithoutSendingInTheRequest()
    {
        var me = await ProvisionAsync($"sub-{Guid.NewGuid():N}");

        var enqueued = Storage.GetMonitoringApi().EnqueuedJobs(JobQueues.Critical, 0, 1000);

        // The fast path: the welcome email is in the outbox, and the relay was nudged.
        Assert.Contains(enqueued, job => job.Value.Job.Type == typeof(ProcessOutboxJob));
        // Enqueued, not sent: the request didn't wait on SMTP.
        Assert.DoesNotContain(factory.EmailSender.Sent, e => e.To == me.Email);
    }
}
