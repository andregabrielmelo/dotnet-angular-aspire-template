using System.Diagnostics;
using AppTemplate.Infrastructure.Data;

namespace AppTemplate.Infrastructure.Auditing;

/// <summary>
/// Saves an explicit audit entry at once. It shares the scope's DbContext, so any pending
/// changes are saved with it.
/// </summary>
internal sealed class AuditLog(
    ApplicationDatabaseContext context,
    AuditActorContext actorContext,
    TimeProvider timeProvider
) : IAuditLog
{
    public async Task RecordAsync(
        string action,
        AuditTarget target,
        AuditOutcome outcome,
        CancellationToken cancellationToken
    )
    {
        context.AuditEntries.Add(
            new AuditEntry
            {
                Id = Guid.CreateVersion7(),
                OccurredAtUtc = timeProvider.GetUtcNow(),
                Actor = actorContext.Actor,
                Action = action,
                EntityType = target.Type,
                EntityKey = target.Key,
                Outcome = outcome.ToString().ToLowerInvariant(),
                TraceId = Activity.Current?.TraceId.ToString(),
            }
        );
        await context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Used when auditing is off (<c>Audit:Enabled=false</c>): records nothing.</summary>
internal sealed class NullAuditLog : IAuditLog
{
    public Task RecordAsync(
        string action,
        AuditTarget target,
        AuditOutcome outcome,
        CancellationToken cancellationToken
    ) => Task.CompletedTask;
}
