namespace AppTemplate.UseCases.Auditing.List;

public sealed class ListAuditEntriesHandler(IAuditQueryService _audit, TimeProvider _timeProvider)
    : IQueryHandler<ListAuditEntriesQuery, Result<PagedResult<AuditEntryDto>>>
{
    public async ValueTask<Result<PagedResult<AuditEntryDto>>> Handle(
        ListAuditEntriesQuery query,
        CancellationToken cancellationToken
    )
    {
        var to = query.To ?? _timeProvider.GetUtcNow();
        var from = query.From ?? to - ListAuditEntriesQuery.DefaultRange;

        return await _audit.ListAsync(
            new AuditEntryFilter(query.EntityType, query.EntityKey, query.Actor, from, to),
            query.Page,
            Math.Min(query.PerPage, ListAuditEntriesQuery.MaxPerPage),
            cancellationToken
        );
    }
}
