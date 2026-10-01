using Hangfire;
using Hangfire.Storage;

namespace AppTemplate.Infrastructure.Jobs.Services;

/// <summary>
/// Makes Hangfire's recurring jobs match the <see cref="IRecurringJobDefinition"/>s in code:
/// <list type="bullet">
/// <item>creates or updates every definition's schedule, re-adding jobs someone deleted from the
/// dashboard;</item>
/// <item>removes runner jobs whose definition no longer exists (renamed or deleted in code),
/// which would otherwise keep firing and do nothing, together with their pause state.</item>
/// </list>
/// Pause state (<see cref="PausedRecurringJobs"/>) doesn't affect the schedule, so a paused job is
/// registered like any other and stays paused. Idempotent (keyed by the stable job id), so it runs
/// on every startup of every instance.
/// </summary>
public sealed partial class RecurringJobRegistrar(
    IEnumerable<IRecurringJobDefinition> definitions,
    IRecurringJobManager jobManager,
    JobStorage jobStorage,
    ILogger<RecurringJobRegistrar> logger
)
{
    public Task<int> RegisterAllAsync(CancellationToken cancellationToken)
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

        foreach (var definition in all)
        {
            Schedule(definition.JobId, definition.CronExpression);
            LogRegistered(logger, definition.JobId, definition.CronExpression);
        }

        cancellationToken.ThrowIfCancellationRequested();
        RemoveOrphans(all.Select(d => d.JobId).ToHashSet(StringComparer.Ordinal));

        return Task.FromResult(all.Count);
    }

    /// <summary>
    /// Removes recurring jobs that run through <see cref="RecurringJobRunner"/> but have no
    /// definition any more, together with their pause state. Recurring jobs registered with
    /// Hangfire in any other way are never touched, and neither are jobs whose stored invocation
    /// can't be loaded - there's no proof they're ours (the admin API can still remove them).
    /// </summary>
    private void RemoveOrphans(IReadOnlySet<string> definitionIds)
    {
        using var connection = jobStorage.GetConnection();
        var stored = connection.GetRecurringJobs();

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

        foreach (var jobId in orphanIds)
        {
            jobManager.RemoveIfExists(jobId);
            PausedRecurringJobs.Resume(connection, jobId);
            LogRemovedOrphan(logger, jobId);
        }
    }

    /// <summary>Points the job at <see cref="RecurringJobRunner"/> - only the id is stored.</summary>
    private void Schedule(string jobId, string cronExpression) =>
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
        Message = "Registered recurring job '{JobId}' ({Cron})"
    )]
    private static partial void LogRegistered(ILogger logger, string jobId, string cron);
}
