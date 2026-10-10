using System.Text.Json;
using AppTemplate.Infrastructure.Auditing;
using AppTemplate.UseCases.Auditing;
using AppTemplate.UseCases.Auditing.List;

namespace AppTemplate.Infrastructure.Data.Queries;

public class ListAuditEntriesQueryService : IListAuditEntriesQueryService
{
    private readonly ApplicationDatabaseContext _db;

    public ListAuditEntriesQueryService(ApplicationDatabaseContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<AuditEntryDto>> ListAsync(
        AuditEntryFilter filter,
        int page,
        int perPage,
        CancellationToken cancellationToken
    )
    {
        var query = _db
            .AuditEntries.AsNoTracking()
            .Where(entry => entry.OccurredAtUtc >= filter.From && entry.OccurredAtUtc <= filter.To);
        if (!string.IsNullOrEmpty(filter.EntityType))
        {
            query = query.Where(entry => entry.EntityType == filter.EntityType);
        }
        if (!string.IsNullOrEmpty(filter.EntityKey))
        {
            query = query.Where(entry => entry.EntityKey == filter.EntityKey);
        }
        if (!string.IsNullOrEmpty(filter.Actor))
        {
            query = query.Where(entry => entry.Actor == filter.Actor);
        }

        var total = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(total / (double)perPage);
        var entries = await query
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .ThenByDescending(entry => entry.Id)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToListAsync(cancellationToken);
        var items = entries
            .Select(entry => new AuditEntryDto(
                entry.Id,
                entry.OccurredAtUtc,
                entry.Actor,
                entry.Action,
                entry.EntityType,
                entry.EntityKey,
                entry.Outcome,
                entry.Changes is null
                    ? null
                    : JsonSerializer.Deserialize<Dictionary<string, AuditValueChange>>(
                        entry.Changes,
                        AuditEntry.ChangesJsonOptions
                    ),
                entry.TraceId
            ))
            .ToList();

        return new PagedResult<AuditEntryDto>(items, page, perPage, total, totalPages);
    }
}
