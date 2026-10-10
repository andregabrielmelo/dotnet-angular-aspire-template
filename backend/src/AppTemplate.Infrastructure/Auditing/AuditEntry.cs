using System.Text.Json;

namespace AppTemplate.Infrastructure.Auditing;

/// <summary>One audited action (table <c>audit_entries</c>). Append-only: nothing updates a row.</summary>
public sealed class AuditEntry
{
    /// <summary>What a personal or secret value is recorded as.</summary>
    public const string Masked = "[redacted]";

    /// <summary>
    /// The storage format of <see cref="Changes"/>, shared by the writer and every reader. Kept
    /// apart from the API's JSON settings: stored rows must stay readable when those change.
    /// </summary>
    public static readonly JsonSerializerOptions ChangesJsonOptions = JsonSerializerOptions.Web;

    public Guid Id { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary><c>user:{sub}</c>, <c>system:{job id}</c> or <c>anonymous</c>.</summary>
    public required string Actor { get; init; }

    /// <summary>See <c>AuditActions</c>: <c>created</c>, <c>job.paused</c>, ...</summary>
    public required string Action { get; init; }

    public required string EntityType { get; init; }

    public required string EntityKey { get; init; }

    /// <summary><c>succeeded</c>, <c>failed</c> or <c>denied</c>.</summary>
    public required string Outcome { get; init; }

    /// <summary>JSON: allowlisted property name to { old, new }; personal data masked.</summary>
    public string? Changes { get; init; }

    public string? TraceId { get; init; }
}
