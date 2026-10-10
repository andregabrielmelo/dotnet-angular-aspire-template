using Hangfire;

namespace AppTemplate.Infrastructure.Outbox;

/// <summary>
/// The fast path: enqueued right after a <c>SaveChanges</c> that wrote outbox messages, so
/// they're usually delivered within moments. Not retried by Hangfire: a failed message is
/// rescheduled in the outbox itself, and <see cref="OutboxSweepJob"/> delivers it.
/// </summary>
public sealed class ProcessOutboxJob(OutboxProcessor processor)
{
    [AutomaticRetry(Attempts = 0)]
    [JobDisplayName("Deliver outbox messages")]
    public Task ExecuteAsync(CancellationToken cancellationToken) =>
        processor.ProcessPendingAsync(cancellationToken);
}
