using AppTemplate.UseCases.Jobs;
using Ardalis.Result;
using Hangfire;
using Hangfire.Storage;
using RecurringJobDto = AppTemplate.UseCases.Jobs.RecurringJobDto;
using StoredRecurringJob = Hangfire.Storage.RecurringJobDto;

namespace AppTemplate.Infrastructure.Jobs.Services;

/// <summary>
/// <see cref="IJobManagementService"/> on Hangfire. Storage, the recurring job manager and the
/// client come from DI (never Hangfire's static APIs). Pause state is a set in Hangfire storage
/// (<see cref="PausedRecurringJobs"/>), so every API instance sees the same state, and pausing
/// never changes a job's schedule.
/// </summary>
public sealed partial class JobManagementService(
    JobStorage jobStorage,
    IRecurringJobManager jobManager,
    IBackgroundJobClient jobClient,
    RecurringJobRegistrar registrar,
    ILogger<JobManagementService> logger
) : IJobManagementService
{
    /// <summary>How many recent runs the detail view shows.</summary>
    public const int HistoryLength = 10;

    public Task<IReadOnlyList<RecurringJobDto>> GetRecurringJobsAsync(
        CancellationToken cancellationToken
    )
    {
        using var connection = jobStorage.GetConnection();
        var paused = PausedRecurringJobs.GetAll(connection);
        IReadOnlyList<RecurringJobDto> jobs = connection
            .GetRecurringJobs()
            .OrderBy(job => job.Id, StringComparer.Ordinal)
            .Select(job => ToDto(job, paused))
            .ToList();
        return Task.FromResult(jobs);
    }

    public Task<Result<RecurringJobDetailDto>> GetRecurringJobAsync(
        string jobId,
        CancellationToken cancellationToken
    )
    {
        using var connection = jobStorage.GetConnection();
        var job = FindScheduledJob(connection, jobId);
        if (job is null)
        {
            return Task.FromResult<Result<RecurringJobDetailDto>>(Result.NotFound());
        }

        var detail = new RecurringJobDetailDto(
            ToDto(job, PausedRecurringJobs.GetAll(connection)),
            GetRecentExecutions(jobId)
        );
        return Task.FromResult<Result<RecurringJobDetailDto>>(detail);
    }

    public Task<Result> TriggerAsync(string jobId, CancellationToken cancellationToken)
    {
        using var connection = jobStorage.GetConnection();
        if (FindScheduledJob(connection, jobId) is null)
        {
            return Task.FromResult(Result.NotFound());
        }

        if (PausedRecurringJobs.IsPaused(connection, jobId))
        {
            // Hangfire's own trigger creates the run from the recurring job, which the pause
            // filter would cancel. A run enqueued directly isn't tied to the recurring job, so an
            // admin can still run a paused job once on purpose.
            jobClient.Enqueue<RecurringJobRunner>(
                JobQueues.Default,
                runner => runner.ExecuteAsync(jobId, CancellationToken.None)
            );
        }
        else
        {
            jobManager.Trigger(jobId);
        }

        LogTriggered(logger, jobId);
        return Task.FromResult(Result.Success());
    }

    public Task<Result> PauseAsync(string jobId, CancellationToken cancellationToken)
    {
        using var connection = jobStorage.GetConnection();
        if (FindScheduledJob(connection, jobId) is null)
        {
            return Task.FromResult(Result.NotFound());
        }

        PausedRecurringJobs.Pause(connection, jobId);
        LogPaused(logger, jobId);
        return Task.FromResult(Result.Success());
    }

    public Task<Result> ResumeAsync(string jobId, CancellationToken cancellationToken)
    {
        using var connection = jobStorage.GetConnection();
        if (FindScheduledJob(connection, jobId) is null)
        {
            return Task.FromResult(Result.NotFound());
        }

        PausedRecurringJobs.Resume(connection, jobId);
        LogResumed(logger, jobId);
        return Task.FromResult(Result.Success());
    }

    public Task<Result> RemoveAsync(string jobId, CancellationToken cancellationToken)
    {
        using var connection = jobStorage.GetConnection();
        if (FindScheduledJob(connection, jobId) is null)
        {
            return Task.FromResult(Result.NotFound());
        }

        jobManager.RemoveIfExists(jobId);
        PausedRecurringJobs.Resume(connection, jobId);
        LogRemoved(logger, jobId);
        return Task.FromResult(Result.Success());
    }

    public async Task<Result> RestoreAsync(CancellationToken cancellationToken)
    {
        await registrar.RegisterAllAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>Hangfire reports an unknown id as a job with <c>Removed</c> set.</summary>
    private static StoredRecurringJob? FindScheduledJob(
        IStorageConnection connection,
        string jobId
    ) => connection.GetRecurringJobs([jobId]).FirstOrDefault(job => !job.Removed);

    private static RecurringJobDto ToDto(StoredRecurringJob job, IReadOnlySet<string> paused)
    {
        var isPaused = paused.Contains(job.Id);
        return new RecurringJobDto(
            job.Id,
            job.Cron,
            isPaused ? null : Utc(job.NextExecution),
            Utc(job.LastExecution),
            job.LastJobState,
            isPaused,
            Utc(job.CreatedAt)
        );
    }

    /// <summary>
    /// Every recurring job runs through <see cref="RecurringJobRunner"/>, so runs are told apart
    /// by their first argument (the job id), not by type.
    /// </summary>
    private List<JobExecutionDto> GetRecentExecutions(string jobId)
    {
        var monitoring = jobStorage.GetMonitoringApi();

        bool IsRunOf(Hangfire.Common.Job? job) =>
            job?.Type == typeof(RecurringJobRunner)
            && job.Args.Count > 0
            && job.Args[0] as string == jobId;

        var succeeded = monitoring
            .SucceededJobs(0, 100)
            .Where(entry => IsRunOf(entry.Value.Job))
            .Select(entry => new JobExecutionDto(
                entry.Key,
                "Succeeded",
                Utc(entry.Value.SucceededAt),
                entry.Value.TotalDuration is { } ms ? TimeSpan.FromMilliseconds(ms) : null,
                null
            ));

        var failed = monitoring
            .FailedJobs(0, 100)
            .Where(entry => IsRunOf(entry.Value.Job))
            .Select(entry => new JobExecutionDto(
                entry.Key,
                "Failed",
                Utc(entry.Value.FailedAt),
                null,
                entry.Value.ExceptionMessage
            ));

        return succeeded
            .Concat(failed)
            .OrderByDescending(execution => execution.FinishedAt)
            .Take(HistoryLength)
            .ToList();
    }

    /// <summary>Hangfire reports UTC <see cref="DateTime"/>s; make that explicit.</summary>
    private static DateTimeOffset? Utc(DateTime? value) =>
        value is { } dateTime
            ? new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc))
            : null;

    [LoggerMessage(Level = LogLevel.Information, Message = "Triggered recurring job '{JobId}'")]
    private static partial void LogTriggered(ILogger logger, string jobId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Paused recurring job '{JobId}'")]
    private static partial void LogPaused(ILogger logger, string jobId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resumed recurring job '{JobId}'")]
    private static partial void LogResumed(ILogger logger, string jobId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Removed recurring job '{JobId}'")]
    private static partial void LogRemoved(ILogger logger, string jobId);
}
