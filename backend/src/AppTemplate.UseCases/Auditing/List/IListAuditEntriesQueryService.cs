namespace AppTemplate.UseCases.Auditing.List;

/// <summary>Reads a filtered page of the audit log (implemented in Infrastructure).</summary>
public interface IListAuditEntriesQueryService
{
    Task<PagedResult<AuditEntryDto>> ListAsync(
        AuditEntryFilter filter,
        int page,
        int perPage,
        CancellationToken cancellationToken
    );
}
