using System.Diagnostics;
using Hangfire;

namespace AppTemplate.Infrastructure.Jobs;

/// <summary>
/// The single Hangfire entry point for every <see cref="IRecurringJobDefinition"/>. Hangfire
/// stores only the job id (a small, primitive argument) and activates this class from DI in a
/// fresh scope per run, so each definition gets properly scoped dependencies - without the
/// static root service provider a static entry point would need.
/// </summary>
public sealed partial class RecurringJobRunner(
    IEnumerable<IRecurringJobDefinition> definitions,
    ILogger<RecurringJobRunner> logger
)
{
    /// <summary>
    /// Few retries (Hangfire's default is 10): a recurring job runs again on its next schedule
    /// anyway. Runs of the same job never overlap - the lock resource includes the job id
    /// (argument {0}), so different jobs still run in parallel.
    /// </summary>
    [AutomaticRetry(Attempts = 2, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    [DisableConcurrentExecution("recurring-job:{0}", 10 * 60)]
    [JobDisplayName("Recurring job: {0}")]
    public async Task ExecuteAsync(string jobId, CancellationToken cancellationToken)
    {
        var definition = definitions.FirstOrDefault(d => d.JobId == jobId);
        if (definition is null)
        {
            // Scheduled in storage but no longer defined in code (e.g. removed in a newer
            // version). Retrying can't help; RestoreAsync / a redeploy cleans it up.
            LogUnknownJob(logger, jobId);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await definition.ExecuteAsync(cancellationToken);
            LogCompleted(logger, jobId, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailed(logger, exception, jobId, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No recurring job definition '{JobId}'; skipping"
    )]
    private static partial void LogUnknownJob(ILogger logger, string jobId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Recurring job '{JobId}' completed in {ElapsedMs} ms"
    )]
    private static partial void LogCompleted(ILogger logger, string jobId, long elapsedMs);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Recurring job '{JobId}' failed after {ElapsedMs} ms"
    )]
    private static partial void LogFailed(
        ILogger logger,
        Exception exception,
        string jobId,
        long elapsedMs
    );
}
