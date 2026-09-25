using AppTemplate.Infrastructure.Data;
using Hangfire;

namespace AppTemplate.Infrastructure.Jobs.Services;

/// <summary>
/// Creates or updates the Hangfire schedule of every <see cref="IRecurringJobDefinition"/>.
/// Idempotent (keyed by the stable job id), so it runs on every startup of every instance, and
/// re-adds jobs someone deleted from the dashboard. Paused jobs (a row in <c>paused_jobs</c>)
/// are scheduled with <see cref="Cron.Never"/>, so a pause survives restarts.
/// </summary>
public sealed partial class RecurringJobRegistrar(
    IEnumerable<IRecurringJobDefinition> definitions,
    IRecurringJobManager jobManager,
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

        return all.Count;
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
