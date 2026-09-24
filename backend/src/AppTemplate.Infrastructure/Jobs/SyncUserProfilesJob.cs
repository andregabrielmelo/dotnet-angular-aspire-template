using AppTemplate.UseCases.Users.SyncProfiles;
using Hangfire;
using Mediator;

namespace AppTemplate.Infrastructure.Jobs;

/// <summary>
/// Recurring Hangfire adapter for <see cref="SyncUserProfilesCommand"/>. Only one run at a time
/// (a slow run must not overlap the next schedule), with few retries: the next scheduled run
/// catches up anyway.
/// </summary>
public sealed partial class SyncUserProfilesJob(
    IMediator mediator,
    ILogger<SyncUserProfilesJob> logger
)
{
    public const string RecurringJobId = "sync-user-profiles";

    [DisableConcurrentExecution(timeoutInSeconds: 10 * 60)]
    [AutomaticRetry(Attempts = 2, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    [JobDisplayName("Sync user profiles from Keycloak")]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SyncUserProfilesCommand(), cancellationToken);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Syncing user profiles failed: {string.Join("; ", result.Errors)}"
            );
        }

        LogSynced(logger, result.Value);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "User profile sync updated {Count} users"
    )]
    private static partial void LogSynced(ILogger logger, int count);
}
