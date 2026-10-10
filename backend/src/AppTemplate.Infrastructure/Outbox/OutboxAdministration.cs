using AppTemplate.Infrastructure.Data;
using AppTemplate.UseCases.Outbox;

namespace AppTemplate.Infrastructure.Outbox;

internal sealed class OutboxAdministration(
    ApplicationDatabaseContext context,
    IOutboxTrigger trigger,
    TimeProvider timeProvider
) : IOutboxAdministration
{
    public async Task<int> RequeueDeadLetteredAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var requeued = await context
            .OutboxMessages.Where(message => message.DeadLetteredAtUtc != null)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(message => message.DeadLetteredAtUtc, (DateTimeOffset?)null)
                        .SetProperty(message => message.Attempts, 0)
                        .SetProperty(message => message.NextAttemptAtUtc, now)
                        .SetProperty(message => message.LockedUntilUtc, (DateTimeOffset?)null),
                cancellationToken
            );
        if (requeued > 0)
        {
            trigger.MessagesWritten();
        }
        return requeued;
    }
}
