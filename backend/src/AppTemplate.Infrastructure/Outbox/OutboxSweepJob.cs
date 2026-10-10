using AppTemplate.Infrastructure.Jobs;
using Hangfire;

namespace AppTemplate.Infrastructure.Outbox;

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
