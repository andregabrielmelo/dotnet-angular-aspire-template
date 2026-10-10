using AppTemplate.Infrastructure.Jobs;
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

/// <summary>
/// The recovery path: every minute, delivers whatever is due. That covers messages whose fast
/// path never ran (the process stopped right after the commit), retries after back-off, and
/// messages whose worker died holding the lease.
/// </summary>
public sealed class OutboxSweepJob(OutboxProcessor processor) : IRecurringJobDefinition
{
    public const string Id = "outbox-sweep";

    public string JobId => Id;

    public string CronExpression => Cron.Minutely();

    public Task ExecuteAsync(CancellationToken cancellationToken) =>
        processor.ProcessPendingAsync(cancellationToken);
}
