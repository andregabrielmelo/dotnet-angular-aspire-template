using AppTemplate.Core.Enums;

namespace AppTemplate.Core.Interfaces;

/// <summary>
/// Records security-relevant actions that aren't entity changes (those are recorded
/// automatically for <c>IAuditable</c> entities). The entry is saved immediately, attributed to
/// the current actor: the signed-in user, a background job, or anonymous.
/// Implemented by <c>AuditLog</c> (Infrastructure, Postgres). Used by the job and outbox command handlers and by Web's denied-request handler.
/// </summary>
public interface IAuditLog
{
    Task RecordAsync(
        string action,
        AuditTarget target,
        AuditOutcome outcome,
        CancellationToken cancellationToken
    );
}

/// <summary>What an action was done to. Keep both values bounded and free of personal data.</summary>
public sealed record AuditTarget(string Type, string Key)
{
    public static AuditTarget Job(string jobId) => new("job", jobId);

    public static AuditTarget Endpoint(string route) => new("endpoint", route);

    public static AuditTarget Outbox() => new("outbox", "dead-letters");
}

/// <summary>Action names: stable, lowercase and dotted, since queries and alerts match on them.</summary>
public static class AuditActions
{
    public const string Created = "created";
    public const string Updated = "updated";
    public const string Deleted = "deleted";

    public const string JobTriggered = "job.triggered";
    public const string JobPaused = "job.paused";
    public const string JobResumed = "job.resumed";
    public const string JobRemoved = "job.removed";
    public const string JobsRestored = "jobs.restored";
    public const string OutboxDeadLettersRequeued = "outbox.dead_letters.requeued";

    /// <summary>A signed-in caller was refused an admin endpoint (403).</summary>
    public const string AuthorizationDenied = "authorization.denied";
}
