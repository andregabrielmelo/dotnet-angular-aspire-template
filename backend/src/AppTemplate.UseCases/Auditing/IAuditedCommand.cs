namespace AppTemplate.UseCases.Auditing;

/// <summary>
/// A command whose every execution is recorded in the audit log by
/// <c>AuditingBehavior</c>: succeeded or failed from its <c>Result</c>, failed when it throws.
/// Use it for security-relevant operator actions (job management, outbox requeues). Keep the
/// target bounded and free of personal data: ids and constants only.
/// </summary>
public interface IAuditedCommand
{
    /// <summary>One of <see cref="AuditActions"/>.</summary>
    string AuditAction { get; }

    AuditTarget AuditTarget { get; }
}
