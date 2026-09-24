using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Users.SendWelcomeEmail;
using Ardalis.Result;
using Hangfire;
using Mediator;

namespace AppTemplate.Infrastructure.Jobs;

/// <summary>
/// Hangfire adapter for <see cref="SendWelcomeEmailCommand"/>. The logic (and its idempotency)
/// lives in the use case; this class only takes a primitive argument, dispatches, and turns
/// failures into exceptions so Hangfire retries them with back-off.
/// </summary>
public sealed partial class WelcomeEmailJob(IMediator mediator, ILogger<WelcomeEmailJob> logger)
{
    [AutomaticRetry(Attempts = 5, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    [JobDisplayName("Send welcome email to user {0}")]
    public async Task RunAsync(int userId, CancellationToken cancellationToken)
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
