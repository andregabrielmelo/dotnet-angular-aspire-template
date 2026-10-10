using System.Diagnostics;
using System.Text.Json;
using AppTemplate.Infrastructure.Data;
using AppTemplate.UseCases.Auditing;

namespace AppTemplate.Infrastructure.Auditing;

/// <summary>
/// Saves an explicit audit entry at once. It shares the scope's DbContext, so any pending
/// changes are saved with it.
/// </summary>
internal sealed class AuditLog(
    ApplicationDatabaseContext context,
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
        context.AuditEntries.Add(
            new AuditEntry
            {
                Id = Guid.CreateVersion7(),
                OccurredAtUtc = timeProvider.GetUtcNow(),
                Actor = actorContext.Actor,
                Action = action,
                EntityType = target.Type,
                EntityKey = target.Key,
                Outcome = outcome.ToString().ToLowerInvariant(),
                TraceId = Activity.Current?.TraceId.ToString(),
            }
        );
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

internal sealed class AuditQueryService(ApplicationDatabaseContext context) : IAuditQueryService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<PagedResult<AuditEntryDto>> ListAsync(
        AuditEntryFilter filter,
        int page,
        int perPage,
        CancellationToken cancellationToken
    )
    {
        var query = context
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
                        Json
                    ),
                entry.TraceId
            ))
            .ToList();

        return new PagedResult<AuditEntryDto>(
            items,
            page,
            perPage,
            total,
            totalPages
        );
    }
}
