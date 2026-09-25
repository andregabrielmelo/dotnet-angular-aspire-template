using AppTemplate.Infrastructure.Data;
using AppTemplate.Infrastructure.Jobs.Models;
using AppTemplate.UseCases.Jobs;
using Ardalis.Result;
using Hangfire;
using Hangfire.Storage;
using RecurringJobDto = AppTemplate.UseCases.Jobs.RecurringJobDto;
using StoredRecurringJob = Hangfire.Storage.RecurringJobDto;

namespace AppTemplate.Infrastructure.Jobs.Services;

/// <summary>
/// <see cref="IJobManagementService"/> on Hangfire. Storage and the recurring job manager come
/// from DI (never Hangfire's static APIs), and pause state lives only in the
/// <c>paused_jobs</c> table, so every API instance sees the same state.
/// </summary>
public sealed partial class JobManagementService(
    JobStorage jobStorage,
    IRecurringJobManager jobManager,
    RecurringJobRegistrar registrar,
    IEnumerable<IRecurringJobDefinition> definitions,
    ApplicationDatabaseContext dbContext,
    TimeProvider timeProvider,
    ILogger<JobManagementService> logger
) : IJobManagementService
{
    /// <summary>How many recent runs the detail view shows.</summary>
    public const int HistoryLength = 10;

    public async Task<IReadOnlyList<RecurringJobDto>> GetRecurringJobsAsync(
        CancellationToken cancellationToken
    )
    {
        var paused = await GetPausedCronsAsync(cancellationToken);
        return GetScheduledJobs()
            .OrderBy(job => job.Id, StringComparer.Ordinal)
            .Select(job => ToDto(job, paused))
            .ToList();
    }

    public async Task<Result<RecurringJobDetailDto>> GetRecurringJobAsync(
        string jobId,
        CancellationToken cancellationToken
    )
    {
        var job = FindScheduledJob(jobId);
        if (job is null)
        {
            return Result.NotFound();
        }

        var paused = await GetPausedCronsAsync(cancellationToken);
        return new RecurringJobDetailDto(ToDto(job, paused), GetRecentExecutions(jobId));
    }

    public Task<Result> TriggerAsync(string jobId, CancellationToken cancellationToken)
    {
        if (FindScheduledJob(jobId) is null)
        {
            return Task.FromResult(Result.NotFound());
        }

        jobManager.Trigger(jobId);
        LogTriggered(logger, jobId);
        return Task.FromResult(Result.Success());
    }

    public async Task<Result> PauseAsync(string jobId, CancellationToken cancellationToken)
    {
        var job = FindScheduledJob(jobId);
        if (job is null)
        {
            return Result.NotFound();
        }

        if (await dbContext.PausedJobs.AnyAsync(p => p.JobId == jobId, cancellationToken))
        {
            return Result.Success();
        }

        // Remember the definition's schedule when there is one - the stored cron may already be
        // Never (e.g. paused by an older instance whose row was removed by hand).
        var originalCron = FindDefinition(jobId)?.CronExpression ?? job.Cron;
        dbContext.PausedJobs.Add(
            new PausedJob
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                OriginalCron = originalCron,
                PausedAtUtc = timeProvider.GetUtcNow(),
            }
        );
        await dbContext.SaveChangesAsync(cancellationToken);

        registrar.Schedule(jobId, Cron.Never());
        LogPaused(logger, jobId, originalCron);
        return Result.Success();
    }

    public async Task<Result> ResumeAsync(string jobId, CancellationToken cancellationToken)
    {
        if (FindScheduledJob(jobId) is null)
        {
            return Result.NotFound();
        }

        var pausedJob = await dbContext.PausedJobs.FirstOrDefaultAsync(
            p => p.JobId == jobId,
            cancellationToken
        );
        if (pausedJob is null)
        {
            return Result.Success();
        }

        dbContext.PausedJobs.Remove(pausedJob);
        await dbContext.SaveChangesAsync(cancellationToken);

        // The definition wins, so a schedule changed in code since the pause takes effect.
        var cron = FindDefinition(jobId)?.CronExpression ?? pausedJob.OriginalCron;
        registrar.Schedule(jobId, cron);
        LogResumed(logger, jobId, cron);
        return Result.Success();
    }

    public async Task<Result> RemoveAsync(string jobId, CancellationToken cancellationToken)
    {
        if (FindScheduledJob(jobId) is null)
        {
            return Result.NotFound();
        }

        jobManager.RemoveIfExists(jobId);

        var pausedJob = await dbContext.PausedJobs.FirstOrDefaultAsync(
            p => p.JobId == jobId,
            cancellationToken
        );
        if (pausedJob is not null)
        {
            dbContext.PausedJobs.Remove(pausedJob);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        LogRemoved(logger, jobId);
        return Result.Success();
    }

    public async Task<Result> RestoreAsync(CancellationToken cancellationToken)
    {
        await registrar.RegisterAllAsync(cancellationToken);
        return Result.Success();
    }

    private IRecurringJobDefinition? FindDefinition(string jobId) =>
        definitions.FirstOrDefault(d => d.JobId == jobId);

    private List<StoredRecurringJob> GetScheduledJobs()
    {
        using var connection = jobStorage.GetConnection();
        return connection.GetRecurringJobs();
    }

    private StoredRecurringJob? FindScheduledJob(string jobId) =>
        GetScheduledJobs().FirstOrDefault(job => job.Id == jobId);

    private async Task<Dictionary<string, string>> GetPausedCronsAsync(
        CancellationToken cancellationToken
    ) =>
        await dbContext
            .PausedJobs.AsNoTracking()
            .ToDictionaryAsync(p => p.JobId, p => p.OriginalCron, cancellationToken);

    private static RecurringJobDto ToDto(
        StoredRecurringJob job,
        IReadOnlyDictionary<string, string> pausedCrons
    )
    {
        var isPaused = pausedCrons.TryGetValue(job.Id, out var originalCron);
        return new RecurringJobDto(
            job.Id,
            isPaused ? originalCron! : job.Cron,
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

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Paused recurring job '{JobId}' (was {Cron})"
    )]
    private static partial void LogPaused(ILogger logger, string jobId, string cron);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Resumed recurring job '{JobId}' ({Cron})"
    )]
    private static partial void LogResumed(ILogger logger, string jobId, string cron);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Removed recurring job '{JobId}'")]
    private static partial void LogRemoved(ILogger logger, string jobId);
}
