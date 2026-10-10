using AppTemplate.Infrastructure.Data;
using AppTemplate.Infrastructure.Jobs;
using Hangfire;

namespace AppTemplate.Infrastructure.Idempotency;

/// <summary>Hourly: deletes expired idempotency records, freeing their keys.</summary>
public sealed class IdempotencyCleanupJob(
    ApplicationDatabaseContext context,
    TimeProvider timeProvider
) : IRecurringJobDefinition
{
    public const string Id = "idempotency-cleanup";

    public string JobId => Id;

    public string CronExpression => Cron.Hourly();

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return context
            .IdempotencyRecords.Where(record => record.ExpiresAtUtc < now)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
