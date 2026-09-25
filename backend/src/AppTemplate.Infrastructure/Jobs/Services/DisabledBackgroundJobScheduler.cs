using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Jobs;

namespace AppTemplate.Infrastructure.Jobs.Services;

/// <summary>
/// Used when <c>JobScheduling:Enabled</c> is false: nothing is enqueued, so the application keeps
/// working without Hangfire - background work is skipped, loudly.
/// </summary>
public sealed partial class DisabledBackgroundJobScheduler(
    ILogger<DisabledBackgroundJobScheduler> logger
) : IBackgroundJobScheduler
{
    public string EnqueueWelcomeEmail(UserId userId)
    {
        LogSkipped(logger, "welcome email", userId.Value);
        return string.Empty;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Job scheduling is disabled; not enqueuing the {Job} for user {UserId}"
    )]
    private static partial void LogSkipped(ILogger logger, string job, int userId);
}
