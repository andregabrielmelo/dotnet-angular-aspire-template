using System.Diagnostics;
using AppTemplate.Infrastructure.Data;

namespace AppTemplate.Infrastructure.Auditing;

/// <summary>
/// Saves an explicit audit entry at once, on its own short-lived DbContext. It never shares
/// the caller's context: when a command fails, whatever it left tracked must not be committed
/// by the audit record of that failure. The actor still comes from the caller's scope.
/// </summary>
internal sealed class AuditLog(
    IServiceScopeFactory scopeFactory,
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
        var entry = new AuditEntry
        {
            Id = Guid.CreateVersion7(),
            OccurredAtUtc = timeProvider.GetUtcNow(),
            Actor = actorContext.Actor,
            Action = action,
            EntityType = target.Type,
            EntityKey = target.Key,
            Outcome = outcome.ToString().ToLowerInvariant(),
            TraceId = Activity.Current?.TraceId.ToString(),
        };

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDatabaseContext>();
        context.AuditEntries.Add(entry);
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
