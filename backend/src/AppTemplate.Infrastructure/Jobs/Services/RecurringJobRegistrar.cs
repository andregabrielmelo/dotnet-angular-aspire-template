using AppTemplate.Infrastructure.Data;
using Hangfire;
using Hangfire.Storage;

namespace AppTemplate.Infrastructure.Jobs.Services;

/// <summary>
/// Makes Hangfire's recurring jobs match the <see cref="IRecurringJobDefinition"/>s in code:
/// <list type="bullet">
/// <item>creates or updates every definition's schedule, re-adding jobs someone deleted from the
/// dashboard;</item>
/// <item>schedules paused jobs (a row in <c>paused_jobs</c>) with <see cref="Cron.Never"/>, so a
/// pause survives restarts;</item>
/// <item>removes runner jobs whose definition no longer exists (renamed or deleted in code),
/// which would otherwise keep firing and do nothing.</item>
/// </list>
/// Idempotent (keyed by the stable job id), so it runs on every startup of every instance.
/// </summary>
public sealed partial class RecurringJobRegistrar(
    IEnumerable<IRecurringJobDefinition> definitions,
    IRecurringJobManager jobManager,
    JobStorage jobStorage,
    ApplicationDatabaseContext dbContext,
    ILogger<RecurringJobRegistrar> logger
)
{
    public async Task<int> RegisterAllAsync(CancellationToken cancellationToken)
    {
        var all = definitions.ToList();

        var duplicate = all.GroupBy(d => d.JobId).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Recurring job id '{duplicate.Key}' is defined more than once."
            );
        }

        if (all.Count == 0)
        {
            LogNoJobs(logger);
        }

        var pausedJobIds = (
            await dbContext
                .PausedJobs.AsNoTracking()
                .Select(p => p.JobId)
                .ToListAsync(cancellationToken)
        ).ToHashSet(StringComparer.Ordinal);

        foreach (var definition in all)
        {
            var isPaused = pausedJobIds.Contains(definition.JobId);
            Schedule(definition.JobId, isPaused ? Cron.Never() : definition.CronExpression);
            LogRegistered(logger, definition.JobId, definition.CronExpression, isPaused);
        }

        await RemoveOrphansAsync(
            all.Select(d => d.JobId).ToHashSet(StringComparer.Ordinal),
            cancellationToken
        );

        return all.Count;
    }

    /// <summary>
    /// Removes recurring jobs that run through <see cref="RecurringJobRunner"/> but have no
    /// definition any more, together with their pause state. Recurring jobs registered with
    /// Hangfire in any other way are never touched, and neither are jobs whose stored invocation
    /// can't be loaded - there's no proof they're ours (the admin API can still remove them).
    /// </summary>
    private async Task RemoveOrphansAsync(
        IReadOnlySet<string> definitionIds,
        CancellationToken cancellationToken
    )
    {
        List<RecurringJobDto> stored;
        using (var connection = jobStorage.GetConnection())
        {
            stored = connection.GetRecurringJobs();
        }

        var orphanIds = new List<string>();
        foreach (var job in stored)
        {
            if (definitionIds.Contains(job.Id))
            {
                continue;
            }

            if (job.Job is null)
            {
                LogUnloadableJob(logger, job.Id);
            }
            else if (job.Job.Type == typeof(RecurringJobRunner))
            {
                orphanIds.Add(job.Id);
            }
        }

        if (orphanIds.Count == 0)
        {
            return;
        }

        foreach (var jobId in orphanIds)
        {
            jobManager.RemoveIfExists(jobId);
            LogRemovedOrphan(logger, jobId);
        }

        var orphanPauses = await dbContext
            .PausedJobs.Where(p => orphanIds.Contains(p.JobId))
            .ToListAsync(cancellationToken);
        if (orphanPauses.Count > 0)
        {
            dbContext.PausedJobs.RemoveRange(orphanPauses);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Points the job at <see cref="RecurringJobRunner"/> - only the id is stored.</summary>
    public void Schedule(string jobId, string cronExpression) =>
        jobManager.AddOrUpdate<RecurringJobRunner>(
            jobId,
            JobQueues.Default,
            runner => runner.ExecuteAsync(jobId, CancellationToken.None),
            cronExpression,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc }
        );

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Removed recurring job '{JobId}': it no longer has a definition"
    )]
    private static partial void LogRemovedOrphan(ILogger logger, string jobId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Recurring job '{JobId}' has no definition and its stored invocation can't be loaded; leaving it"
    )]
    private static partial void LogUnloadableJob(ILogger logger, string jobId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No recurring job definitions registered")]
    private static partial void LogNoJobs(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Registered recurring job '{JobId}' ({Cron}), paused: {IsPaused}"
    )]
    private static partial void LogRegistered(
        ILogger logger,
        string jobId,
        string cron,
        bool isPaused
    );
}
