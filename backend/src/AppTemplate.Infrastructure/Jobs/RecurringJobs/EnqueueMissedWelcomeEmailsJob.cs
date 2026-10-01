using AppTemplate.UseCases.Users.SendWelcomeEmail;
using Hangfire;
using Mediator;

namespace AppTemplate.Infrastructure.Jobs.RecurringJobs;

/// <summary>
/// Hourly: re-enqueues welcome emails that were never enqueued (see
/// <see cref="EnqueueMissedWelcomeEmailsHandler"/>). Safe to repeat: the email job itself
/// checks whether the email already went out.
/// </summary>
public sealed partial class EnqueueMissedWelcomeEmailsJob(
    IMediator mediator,
    ILogger<EnqueueMissedWelcomeEmailsJob> logger
) : IRecurringJobDefinition
{
    public const string Id = "enqueue-missed-welcome-emails";

    public string JobId => Id;

    public string CronExpression => Cron.Hourly();

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new EnqueueMissedWelcomeEmailsCommand(),
            cancellationToken
        );
        if (!result.IsSuccess)
        {
            // Throwing marks the run as failed, so Hangfire retries it.
            throw new InvalidOperationException(
                $"Enqueueing missed welcome emails failed: {string.Join("; ", result.Errors)}"
            );
        }

        if (result.Value > 0)
        {
            LogEnqueued(logger, result.Value);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Enqueued {Count} welcome emails that had been missed"
    )]
    private static partial void LogEnqueued(ILogger logger, int count);
}
