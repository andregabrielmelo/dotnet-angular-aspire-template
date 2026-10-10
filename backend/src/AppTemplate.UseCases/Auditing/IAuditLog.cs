namespace AppTemplate.UseCases.Auditing;

/// <summary>
/// Records security-relevant actions that aren't entity changes (those are recorded
/// automatically for <c>IAuditable</c> entities). The entry is saved immediately, attributed to
/// the current actor: the signed-in user, a background job, or anonymous.
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

public enum AuditOutcome
{
    Succeeded,
    Failed,
    Denied,
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

public static class AuditLogExtensions
{
    /// <summary>Records the outcome of a use case result: success, or failure with no detail.</summary>
    public static Task RecordAsync(
        this IAuditLog auditLog,
        string action,
        AuditTarget target,
        Ardalis.Result.IResult result,
        CancellationToken cancellationToken
    ) =>
        auditLog.RecordAsync(
            action,
            target,
            result.IsOk() ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            cancellationToken
        );

    private static bool IsOk(this Ardalis.Result.IResult result) =>
        result.Status is ResultStatus.Ok or ResultStatus.NoContent or ResultStatus.Created;
}
