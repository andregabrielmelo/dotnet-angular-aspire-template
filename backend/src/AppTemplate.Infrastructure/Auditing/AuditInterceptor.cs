using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using AppTemplate.Infrastructure.Data;
using AppTemplate.UseCases.Auditing;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Auditing;

/// <summary>
/// Adds an <see cref="AuditEntry"/> for every inserted, updated or deleted
/// <see cref="IAuditable"/> entity to the same <c>SaveChanges</c>, so the change and its audit
/// record commit together. If the audit write fails, the whole save fails: an unaudited change
/// never happens. Only allowlisted properties are recorded; an update that touched none of
/// them isn't recorded at all.
/// </summary>
internal sealed class AuditInterceptor(
    IOptions<AuditOptions> options,
    AuditActorContext actorContext,
    TimeProvider timeProvider
) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        if (eventData.Context is ApplicationDatabaseContext context)
        {
            context.ChangeTracker.DetectChanges();
            var entries = context
                .ChangeTracker.Entries()
                .Where(entry =>
                    entry.Entity is IAuditable
                    && entry.State
                        is EntityState.Added
                            or EntityState.Modified
                            or EntityState.Deleted
                    && !entry.Metadata.IsOwned()
                )
                .ToList();

            var now = timeProvider.GetUtcNow();
            foreach (var entry in entries)
            {
                if (ToAuditEntry(entry, now) is { } auditEntry)
                {
                    context.AuditEntries.Add(auditEntry);
                }
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private AuditEntry? ToAuditEntry(EntityEntry entry, DateTimeOffset now)
    {
        var allowlist =
            options.Value.AuditedProperties.GetValueOrDefault(entry.Metadata.ClrType) ?? [];
        var changes = new Dictionary<string, AuditValueChange>(StringComparer.Ordinal);

        foreach (var name in allowlist)
        {
            if (Change(entry, name) is { } change)
            {
                changes[JsonNamingPolicy.CamelCase.ConvertName(name)] = change;
            }
        }

        if (entry.State == EntityState.Modified && changes.Count == 0)
        {
            return null; // only unaudited properties changed
        }

        return new AuditEntry
        {
            Id = Guid.CreateVersion7(),
            OccurredAtUtc = now,
            Actor = actorContext.Actor,
            Action = entry.State switch
            {
                EntityState.Added => AuditActions.Created,
                EntityState.Deleted => AuditActions.Deleted,
                _ => AuditActions.Updated,
            },
            EntityType = entry.Metadata.ClrType.Name,
            EntityKey = KeyOf(entry),
            Outcome = AuditOutcome.Succeeded.ToString().ToLowerInvariant(),
            Changes = changes.Count == 0 ? null : JsonSerializer.Serialize(changes, Json),
            TraceId = Activity.Current?.TraceId.ToString(),
        };
    }

    private static AuditValueChange? Change(EntityEntry entry, string name)
    {
        var navigation = entry.Metadata.FindNavigation(name);
        if (navigation?.TargetEntityType.IsOwned() == true)
        {
            // An owned value object (such as PhoneNumber): changed if its row was added,
            // removed or modified. Recorded as a whole, masked when classified.
            var owned = entry.Reference(name).TargetEntry;
            var changed =
                owned is not null
                && (
                    owned.State is EntityState.Added or EntityState.Deleted
                    || (
                        owned.State == EntityState.Modified
                        && owned.Properties.Any(p => p.IsModified)
                    )
                    || entry.State is EntityState.Added or EntityState.Deleted
                );
            if (!changed)
            {
                return null;
            }
            var mask = IsClassified(navigation.PropertyInfo) || IsClassified(navigation.ClrType);
            return mask
                ? new AuditValueChange(AuditEntry.Masked, AuditEntry.Masked)
                : new AuditValueChange(null, owned?.Entity.ToString());
        }

        var property = entry.Property(name);
        var (old, current) = entry.State switch
        {
            EntityState.Added => (null, property.CurrentValue),
            EntityState.Deleted => (property.OriginalValue, null),
            _ when property.IsModified && !Equals(property.OriginalValue, property.CurrentValue) =>
                (property.OriginalValue, property.CurrentValue),
            _ => ((object?)null, (object?)null),
        };
        if (entry.State == EntityState.Modified && old is null && current is null)
        {
            return null;
        }

        var masked =
            IsClassified(property.Metadata.PropertyInfo) || IsClassified(property.Metadata.ClrType);
        return new AuditValueChange(
            Render(old, masked, entry.State != EntityState.Added),
            Render(current, masked, entry.State != EntityState.Deleted)
        );
    }

    private static string? Render(object? value, bool masked, bool present) =>
        !present ? null
        : masked ? AuditEntry.Masked
        : value?.ToString();

    private static bool IsClassified(MemberInfo? member) =>
        member?.GetCustomAttribute<DataClassificationAttribute>(inherit: true) is not null;

    private static string KeyOf(EntityEntry entry) =>
        string.Join(
            ",",
            entry
                .Metadata.FindPrimaryKey()!
                .Properties.Select(key => entry.Property(key.Name).CurrentValue?.ToString())
        );
}
