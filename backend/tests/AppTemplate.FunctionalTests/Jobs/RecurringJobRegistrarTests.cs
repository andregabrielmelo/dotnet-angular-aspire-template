using AppTemplate.Infrastructure.Data;
using AppTemplate.Infrastructure.Jobs;
using AppTemplate.Infrastructure.Jobs.Models;
using AppTemplate.Infrastructure.Jobs.Services;
using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppTemplate.FunctionalTests.Jobs;

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

    private async Task SetPausedAsync(bool paused)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDatabaseContext>();
        db.PausedJobs.RemoveRange(
            db.PausedJobs.Where(p => p.JobId == TestRecurringJobDefinition.Id)
        );
        await db.SaveChangesAsync();
        if (paused)
        {
            db.PausedJobs.Add(
                new PausedJob
                {
                    Id = Guid.NewGuid(),
                    JobId = TestRecurringJobDefinition.Id,
                    OriginalCron = Cron.Daily(),
                    PausedAtUtc = DateTimeOffset.UtcNow,
                }
            );
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task RegisterAll_KeepsPausedJobsOnANeverFiringSchedule_UntilTheyAreUnpaused()
    {
        try
        {
            // Like an application restart while the job is paused.
            await SetPausedAsync(true);
            await RegisterAllAsync();
            Assert.Equal(Cron.Never(), ScheduledCron(TestRecurringJobDefinition.Id));

            await SetPausedAsync(false);
            await RegisterAllAsync();
            Assert.Equal(Cron.Daily(), ScheduledCron(TestRecurringJobDefinition.Id));
        }
        finally
        {
            await SetPausedAsync(false);
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

    private async Task<bool> HasPauseRowAsync(string jobId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDatabaseContext>();
        return db.PausedJobs.Any(p => p.JobId == jobId);
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
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDatabaseContext>();
            db.PausedJobs.Add(
                new PausedJob
                {
                    Id = Guid.NewGuid(),
                    JobId = orphan,
                    OriginalCron = Cron.Daily(),
                    PausedAtUtc = DateTimeOffset.UtcNow,
                }
            );
            await db.SaveChangesAsync();
        }

        await RegisterAllAsync();

        Assert.False(IsScheduled(orphan));
        Assert.False(await HasPauseRowAsync(orphan));
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
