using AppTemplate.Infrastructure.Data;
using AppTemplate.Infrastructure.Jobs;
using Hangfire;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Auditing;

/// <summary>Daily: deletes audit entries older than <c>Audit:RetentionDays</c>.</summary>
public sealed partial class AuditRetentionJob(
    ApplicationDatabaseContext context,
    IOptions<AuditOptions> options,
    TimeProvider timeProvider,
    ILogger<AuditRetentionJob> logger
) : IRecurringJobDefinition
{
    public const string Id = "audit-retention";

    public string JobId => Id;

    public string CronExpression => Cron.Daily(3);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow().AddDays(-options.Value.RetentionDays);
        var deleted = await context
            .AuditEntries.Where(entry => entry.OccurredAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
        if (deleted > 0)
        {
            LogDeleted(logger, deleted, options.Value.RetentionDays);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Deleted {Count} audit entries older than {RetentionDays} days"
    )]
    private static partial void LogDeleted(ILogger logger, int count, int retentionDays);
}
