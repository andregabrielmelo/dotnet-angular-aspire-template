using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Users.SendWelcomeEmail;
using Ardalis.Result;
using Hangfire;
using Mediator;

namespace AppTemplate.Infrastructure.Jobs.FireAndForget;

/// <summary>
/// Fire-and-forget job for <see cref="SendWelcomeEmailCommand"/>, enqueued through
/// <see cref="Services.HangfireBackgroundJobScheduler"/>. The logic (and its idempotency) lives in
/// the use case; this class only takes a primitive argument, dispatches, and turns failures into
/// exceptions so Hangfire retries them.
/// </summary>
public sealed partial class WelcomeEmailJob(IMediator mediator, ILogger<WelcomeEmailJob> logger)
{
    /// <summary>Retries after the first failure (Hangfire's default is 10).</summary>
    public const int RetryAttempts = 5;

    /// <summary>
    /// Explicit back-off for SMTP outages: 30 s, 2 min, 10 min, 30 min, 1 h. Failed deliveries
    /// then stay in storage (Failed state) and can be retried from the dashboard.
    /// </summary>
    [AutomaticRetry(
        Attempts = RetryAttempts,
        DelaysInSeconds = [30, 120, 600, 1800, 3600],
        OnAttemptsExceeded = AttemptsExceededAction.Fail
    )]
    [JobDisplayName("Send welcome email to user {0}")]
    public async Task ExecuteAsync(int userId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new SendWelcomeEmailCommand(UserId.From(userId)),
            cancellationToken
        );

        if (result.Status == ResultStatus.NotFound)
        {
            // Deleted before the job ran - retrying won't bring the user back.
            LogUserGone(logger, userId);
            return;
        }

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Sending the welcome email to user {userId} failed: {string.Join("; ", result.Errors)}"
            );
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "User {UserId} no longer exists; skipping the welcome email"
    )]
    private static partial void LogUserGone(ILogger logger, int userId);
}
