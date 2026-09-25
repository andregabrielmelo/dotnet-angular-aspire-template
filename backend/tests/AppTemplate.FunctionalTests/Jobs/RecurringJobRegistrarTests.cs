using AppTemplate.Infrastructure.Data;
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
}
