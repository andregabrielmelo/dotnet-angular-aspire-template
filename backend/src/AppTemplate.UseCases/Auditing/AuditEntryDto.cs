namespace AppTemplate.UseCases.Auditing;

/// <param name="Changes">Property name to old and new value; masked values read "[redacted]".</param>
public sealed record AuditEntryDto(
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

public sealed record AuditValueChange(string? Old, string? New);

/// <summary>Filters on indexed columns only, plus a bounded time range and page.</summary>
public sealed record AuditEntryFilter(
    string? EntityType,
    string? EntityKey,
    string? Actor,
    DateTimeOffset From,
    DateTimeOffset To
);

/// <summary>Reads the audit log (implemented in Infrastructure).</summary>
public interface IAuditQueryService
{
    Task<PagedResult<AuditEntryDto>> ListAsync(
        AuditEntryFilter filter,
        int page,
        int perPage,
        CancellationToken cancellationToken
    );
}
