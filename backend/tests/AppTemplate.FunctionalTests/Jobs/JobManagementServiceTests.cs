using AppTemplate.Infrastructure.Data;
using AppTemplate.Infrastructure.Jobs;
using AppTemplate.Infrastructure.Jobs.RecurringJobs;
using AppTemplate.UseCases.Jobs;
using Ardalis.Result;
using Hangfire;
using Hangfire.AspNetCore;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using RecurringJobDto = AppTemplate.UseCases.Jobs.RecurringJobDto;

namespace AppTemplate.FunctionalTests.Jobs;

/// <summary>
/// Exercises JobManagementService against the test host's in-memory Hangfire storage and
/// database. Tests only change the test recurring job, and each starts from a clean state.
/// </summary>
public class JobManagementServiceTests
    : IClassFixture<AppTemplateWebApplicationFactory>,
        IAsyncLifetime
{
    private const string JobId = TestRecurringJobDefinition.Id;

    private readonly AppTemplateWebApplicationFactory _factory;
    private readonly AsyncServiceScope _scope;
    private readonly IJobManagementService _service;

    public JobManagementServiceTests(AppTemplateWebApplicationFactory factory)
    {
        _factory = factory;
        _scope = factory.Services.CreateAsyncScope();
        _service = _scope.ServiceProvider.GetRequiredService<IJobManagementService>();
    }

    public async Task InitializeAsync()
    {
        await _service.ResumeAsync(JobId, CancellationToken.None);
        await _service.RestoreAsync(CancellationToken.None);
    }

    public async Task DisposeAsync() => await _scope.DisposeAsync();

    private JobStorage Storage => _factory.Services.GetRequiredService<JobStorage>();

    private string StoredCron(string jobId)
    {
        using var connection = Storage.GetConnection();
        return connection.GetRecurringJobs().Single(j => j.Id == jobId).Cron;
    }

    private async Task<int> PausedRows()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope
            .ServiceProvider.GetRequiredService<ApplicationDatabaseContext>()
            .PausedJobs.CountAsync(p => p.JobId == JobId);
    }

    private async Task<RecurringJobDto> GetJob() =>
        (await _service.GetRecurringJobsAsync(CancellationToken.None)).Single(j => j.Id == JobId);

    [Fact]
    public async Task List_ContainsEveryDefinition()
    {
        var jobs = await _service.GetRecurringJobsAsync(CancellationToken.None);

        Assert.Contains(jobs, j => j.Id == SyncUserProfilesJob.Id && j.Cron == "0 * * * *");
        var test = Assert.Single(jobs, j => j.Id == JobId);
        Assert.False(test.IsPaused);
        Assert.NotNull(test.NextExecution);
    }

    [Fact]
    public async Task Pause_StopsTheScheduleButKeepsShowingTheOriginalOne()
    {
        var result = await _service.PauseAsync(JobId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Cron.Never(), StoredCron(JobId));
        var job = await GetJob();
        Assert.True(job.IsPaused);
        Assert.Equal(Cron.Daily(), job.Cron);
        Assert.Null(job.NextExecution);
        Assert.Equal(1, await PausedRows());
    }

    [Fact]
    public async Task Pause_Twice_IsANoOp()
    {
        await _service.PauseAsync(JobId, CancellationToken.None);

        var second = await _service.PauseAsync(JobId, CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Equal(1, await PausedRows());
    }

    [Fact]
    public async Task Resume_RestoresTheSchedule()
    {
        await _service.PauseAsync(JobId, CancellationToken.None);

        var result = await _service.ResumeAsync(JobId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Cron.Daily(), StoredCron(JobId));
        Assert.False((await GetJob()).IsPaused);
        Assert.Equal(0, await PausedRows());
    }

    [Fact]
    public async Task Resume_WhenNotPaused_IsANoOp()
    {
        var result = await _service.ResumeAsync(JobId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Cron.Daily(), StoredCron(JobId));
    }

    [Fact]
    public async Task Trigger_EnqueuesARunOfThatJobOnly()
    {
        var result = await _service.TriggerAsync(JobId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var enqueued = Storage.GetMonitoringApi().EnqueuedJobs(JobQueues.Default, 0, 1000);
        Assert.Contains(
            enqueued,
            e =>
                e.Value.Job.Type == typeof(RecurringJobRunner)
                && (string)e.Value.Job.Args[0] == JobId
        );
    }

    [Fact]
    public async Task Remove_TakesTheJobOutOfTheScheduler_AndRestoreBringsItBack()
    {
        await _service.PauseAsync(JobId, CancellationToken.None);

        Assert.True((await _service.RemoveAsync(JobId, CancellationToken.None)).IsSuccess);
        Assert.DoesNotContain(
            await _service.GetRecurringJobsAsync(CancellationToken.None),
            j => j.Id == JobId
        );
        Assert.Equal(0, await PausedRows()); // removing also forgets the pause

        Assert.True((await _service.RestoreAsync(CancellationToken.None)).IsSuccess);
        Assert.Equal(Cron.Daily(), StoredCron(JobId));
    }

    [Fact]
    public async Task Restore_KeepsPausedJobsPaused()
    {
        await _service.PauseAsync(JobId, CancellationToken.None);
        // As if someone deleted it from the dashboard, then an admin clicked Restore.
        _factory.Services.GetRequiredService<IRecurringJobManager>().RemoveIfExists(JobId);

        await _service.RestoreAsync(CancellationToken.None);

        Assert.Equal(Cron.Never(), StoredCron(JobId));
        Assert.True((await GetJob()).IsPaused);
    }

    [Fact]
    public async Task UnknownJobs_AreNotFound()
    {
        const string unknown = "no-such-job";

        Assert.Equal(
            ResultStatus.NotFound,
            (await _service.GetRecurringJobAsync(unknown, CancellationToken.None)).Status
        );
        Assert.Equal(ResultStatus.NotFound, (await _service.TriggerAsync(unknown, default)).Status);
        Assert.Equal(ResultStatus.NotFound, (await _service.PauseAsync(unknown, default)).Status);
        Assert.Equal(ResultStatus.NotFound, (await _service.ResumeAsync(unknown, default)).Status);
        Assert.Equal(ResultStatus.NotFound, (await _service.RemoveAsync(unknown, default)).Status);
    }

    [Fact]
    public async Task Detail_ShowsARunOnceAServerHasProcessedIt()
    {
        _factory.TestRecurringJob.FailWith = null;
        await _service.TriggerAsync(JobId, CancellationToken.None);

        // The test host runs no server; start a short-lived one over the same storage and DI.
        using (
            new BackgroundJobServer(
                new BackgroundJobServerOptions
                {
                    Queues = [JobQueues.Default],
                    WorkerCount = 1,
                    Activator = new AspNetCoreJobActivator(
                        _factory.Services.GetRequiredService<IServiceScopeFactory>()
                    ),
                },
                Storage
            )
        )
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            RecurringJobDetailDto? detail = null;
            while (DateTime.UtcNow < deadline)
            {
                detail = (await _service.GetRecurringJobAsync(JobId, CancellationToken.None)).Value;
                if (detail.RecentExecutions.Count > 0)
                {
                    break;
                }
                await Task.Delay(100);
            }

            var execution = Assert.Single(detail!.RecentExecutions);
            Assert.Equal("Succeeded", execution.Status);
            Assert.NotNull(execution.FinishedAt);
        }
    }
}
