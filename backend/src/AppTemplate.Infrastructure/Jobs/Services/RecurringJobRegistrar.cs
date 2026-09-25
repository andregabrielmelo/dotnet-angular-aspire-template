using Hangfire;

namespace AppTemplate.Infrastructure.Jobs.Services;

/// <summary>
/// Creates or updates the Hangfire schedule of every <see cref="IRecurringJobDefinition"/>.
/// Idempotent (keyed by the stable job id), so it runs on every startup of every instance.
/// </summary>
public sealed partial class RecurringJobRegistrar(
    IEnumerable<IRecurringJobDefinition> definitions,
    IRecurringJobManager jobManager,
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
            cancellationToken.ThrowIfCancellationRequested();
            Schedule(definition.JobId, definition.CronExpression);
            LogRegistered(logger, definition.JobId, definition.CronExpression);
        }

        return Task.FromResult(all.Count);
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
        Message = "Registered recurring job '{JobId}' ({Cron})"
    )]
    private static partial void LogRegistered(ILogger logger, string jobId, string cron);
}
