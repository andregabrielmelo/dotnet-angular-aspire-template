using AppTemplate.UseCases.Users.SyncProfiles;
using Hangfire;
using Mediator;

namespace AppTemplate.Infrastructure.Jobs.RecurringJobs;

/// <summary>
/// Hourly: copies name and email changes from Keycloak (where users edit them) into the domain
/// users. The logic is <see cref="SyncUserProfilesCommand"/>; this definition only schedules it.
/// Safe to repeat: it copies current values and never deletes anything.
/// </summary>
public sealed partial class SyncUserProfilesJob(
    IMediator mediator,
    ILogger<SyncUserProfilesJob> logger
) : IRecurringJobDefinition
{
    public const string Id = "sync-user-profiles";

    public string JobId => Id;

    public string CronExpression => Cron.Hourly();

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SyncUserProfilesCommand(), cancellationToken);
        if (!result.IsSuccess)
        {
            // Throwing marks the run as failed, so Hangfire retries it.
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
