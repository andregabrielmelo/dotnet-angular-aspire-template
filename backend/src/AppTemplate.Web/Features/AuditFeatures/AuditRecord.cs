using AppTemplate.UseCases.Auditing;

namespace AppTemplate.Web.Features.AuditFeatures;

/// <param name="Changes">Property name to old and new value; masked values read "[redacted]".</param>
public record AuditRecord(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    string Actor,
    string Action,
    string EntityType,
    string EntityKey,
    string Outcome,
    IReadOnlyDictionary<string, AuditValueChange>? Changes,
    string? TraceId
);
