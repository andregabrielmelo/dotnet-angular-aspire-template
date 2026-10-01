using AppTemplate.Infrastructure.Jobs;
using AppTemplate.Infrastructure.Jobs.Services;
using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppTemplate.FunctionalTests.Jobs;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class RecurringJobRegistrarTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    private string ScheduledCron(string jobId)
    {
        using var connection = factory.Services.GetRequiredService<JobStorage>().GetConnection();
        return connection.GetRecurringJobs().Single(j => j.Id == jobId).Cron;
    }

    private async Task RegisterAllAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope
            .ServiceProvider.GetRequiredService<RecurringJobRegistrar>()
            .RegisterAllAsync(CancellationToken.None);
    }

    private void SetPaused(string jobId, bool paused)
    {
        using var connection = factory.Services.GetRequiredService<JobStorage>().GetConnection();
        if (paused)
        {
            PausedRecurringJobs.Pause(connection, jobId);
        }
        else
        {
            PausedRecurringJobs.Resume(connection, jobId);
        }
    }

    private bool IsPaused(string jobId)
    {
        using var connection = factory.Services.GetRequiredService<JobStorage>().GetConnection();
        return PausedRecurringJobs.IsPaused(connection, jobId);
    }

    [Fact]
    public async Task RegisterAll_KeepsPausedJobsPaused_OnTheirRealSchedule()
    {
        try
        {
            // Like an application restart while the job is paused.
            SetPaused(TestRecurringJobDefinition.Id, true);
            await RegisterAllAsync();

            Assert.Equal(Cron.Daily(), ScheduledCron(TestRecurringJobDefinition.Id));
            Assert.True(IsPaused(TestRecurringJobDefinition.Id));
        }
        finally
        {
            SetPaused(TestRecurringJobDefinition.Id, false);
        }
    }

    [Fact]
    public async Task RegisterAll_RestoresAJobDeletedFromStorage()
    {
        factory
            .Services.GetRequiredService<IRecurringJobManager>()
            .RemoveIfExists(TestRecurringJobDefinition.Id);

        await RegisterAllAsync();

        Assert.Equal(Cron.Daily(), ScheduledCron(TestRecurringJobDefinition.Id));
    }

    private bool IsScheduled(string jobId)
    {
        using var connection = factory.Services.GetRequiredService<JobStorage>().GetConnection();
        return connection.GetRecurringJobs().Any(j => j.Id == jobId);
    }

    [Fact]
    public async Task RegisterAll_RemovesRunnerJobsWhoseDefinitionNoLongerExists()
    {
        // As if a definition had been renamed or deleted in code: still scheduled, and paused.
        const string orphan = "orphan-job";
        var jobManager = factory.Services.GetRequiredService<IRecurringJobManager>();
        jobManager.AddOrUpdate<RecurringJobRunner>(
            orphan,
            JobQueues.Default,
            runner => runner.ExecuteAsync(orphan, CancellationToken.None),
            Cron.Daily()
        );
        SetPaused(orphan, true);

        await RegisterAllAsync();

        Assert.False(IsScheduled(orphan));
        Assert.False(IsPaused(orphan));
        Assert.True(IsScheduled(TestRecurringJobDefinition.Id));
    }

    [Fact]
    public async Task RegisterAll_LeavesRecurringJobsItDoesNotOwn()
    {
        // Registered with Hangfire directly, not through RecurringJobRunner.
        const string foreign = "foreign-job";
        var jobManager = factory.Services.GetRequiredService<IRecurringJobManager>();
        jobManager.AddOrUpdate<TestRecurringJobDefinition>(
            foreign,
            JobQueues.Default,
            job => job.ExecuteAsync(CancellationToken.None),
            Cron.Daily()
        );
        try
        {
            await RegisterAllAsync();

            Assert.True(IsScheduled(foreign));
        }
        finally
        {
            jobManager.RemoveIfExists(foreign);
        }
    }
}
