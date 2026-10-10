using AppTemplate.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AppTemplate.Infrastructure.Outbox;

/// <summary>
/// Writes each integration event raised by a tracked entity as an <see cref="OutboxMessage"/>
/// row, in the <b>same</b> <c>SaveChanges</c> (so the same transaction) as the change itself:
/// either both commit or neither does. Once saved, it nudges the relay (the fast path); if
/// the process dies before that, <see cref="OutboxSweepJob"/> finds the row anyway.
/// </summary>
internal sealed class OutboxInterceptor(
    IntegrationEventRegistry registry,
    IOutboxTrigger trigger,
    TimeProvider timeProvider
) : SaveChangesInterceptor
{
    private bool _wroteMessages;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        if (eventData.Context is ApplicationDatabaseContext context)
        {
            var now = timeProvider.GetUtcNow();
            foreach (
                var entity in context
                    .ChangeTracker.Entries<HasDomainEventsBase>()
                    .Select(entry => entry.Entity)
                    .Where(entity => entity.IntegrationEvents.Count > 0)
                    .ToArray()
            )
            {
                foreach (var integrationEvent in entity.IntegrationEvents)
                {
                    var (key, payload) = registry.Serialize(integrationEvent);
                    context.OutboxMessages.Add(
                        new OutboxMessage
                        {
                            Id = Guid.CreateVersion7(),
                            Type = key,
                            Payload = payload,
                            OccurredAtUtc = now,
                            NextAttemptAtUtc = now,
                        }
                    );
                    _wroteMessages = true;
                }
                entity.ClearIntegrationEvents();
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default
    )
    {
        // Inside an explicit transaction the rows aren't visible until it commits; the relay
        // then finds nothing yet and the sweep picks them up within a minute.
        if (_wroteMessages)
        {
            _wroteMessages = false;
            trigger.MessagesWritten();
        }
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        _wroteMessages = false;
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }
}
