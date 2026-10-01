using AppTemplate.Infrastructure.Jobs;
using Hangfire;
using Hangfire.InMemory;
using Hangfire.Storage;
using Xunit;

namespace AppTemplate.FunctionalTests.Jobs;

/// <summary>
/// The pause filter against real Hangfire client code: a trigger goes through the same
/// recurring-job path as the scheduler, so a cancelled trigger proves scheduled runs are
/// cancelled too.
/// </summary>
public class SkipWhenPausedAttributeTests
{
    private const string JobId = "paused-filter-test-job";

    private readonly InMemoryStorage _storage = new();
    private readonly RecurringJobManager _jobManager;

    public SkipWhenPausedAttributeTests()
    {
        _jobManager = new RecurringJobManager(_storage);
        _jobManager.AddOrUpdate<RecurringJobRunner>(
            JobId,
            JobQueues.Default,
            runner => runner.ExecuteAsync(JobId, CancellationToken.None),
            Cron.Daily()
        );
    }

    private void SetPaused(bool paused)
    {
        using var connection = _storage.GetConnection();
        if (paused)
        {
            PausedRecurringJobs.Pause(connection, JobId);
        }
        else
        {
            PausedRecurringJobs.Resume(connection, JobId);
        }
    }

    private long EnqueuedCount() => _storage.GetMonitoringApi().EnqueuedCount(JobQueues.Default);

    [Fact]
    public void RecurringRun_OfAPausedJob_IsNotCreated_AndTheScheduleIsKept()
    {
        SetPaused(true);

        var backgroundJobId = _jobManager.TriggerJob(JobId);

        Assert.True(string.IsNullOrEmpty(backgroundJobId));
        Assert.Equal(0, EnqueuedCount());
        using var connection = _storage.GetConnection();
        Assert.Equal(Cron.Daily(), connection.GetRecurringJobs([JobId]).Single().Cron);
    }

    [Fact]
    public void RecurringRun_AfterResume_IsCreated()
    {
        SetPaused(true);
        SetPaused(false);

        var backgroundJobId = _jobManager.TriggerJob(JobId);

        Assert.False(string.IsNullOrEmpty(backgroundJobId));
        Assert.Equal(1, EnqueuedCount());
    }

    [Fact]
    public void DirectlyEnqueuedRun_OfAPausedJob_IsCreated()
    {
        SetPaused(true);

        new BackgroundJobClient(_storage).Enqueue<RecurringJobRunner>(
            JobQueues.Default,
            runner => runner.ExecuteAsync(JobId, CancellationToken.None)
        );

        Assert.Equal(1, EnqueuedCount());
    }

    [Fact]
    public void PauseAndResume_AreIdempotent()
    {
        SetPaused(true);
        SetPaused(true);
        using (var connection = _storage.GetConnection())
        {
            Assert.Equal([JobId], PausedRecurringJobs.GetAll(connection));
        }

        SetPaused(false);
        SetPaused(false);
        using (var connection = _storage.GetConnection())
        {
            Assert.Empty(PausedRecurringJobs.GetAll(connection));
        }
    }
}
