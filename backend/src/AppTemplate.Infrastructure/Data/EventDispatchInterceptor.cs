using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AppTemplate.Infrastructure.Data;

/// <summary>
/// Dispatches domain events (via Mediator) once SaveChanges has succeeded, so handlers only
/// ever see changes that were actually persisted.
/// </summary>
/// <remarks>
/// Dispatch runs after the save has committed and outside any transaction. A handler that
/// fails does not roll the original change back (the caller sees the exception, but the data
/// is already saved), and anything a handler writes is a separate, later save. So handlers
/// should be idempotent and cheap - ideally just enqueue a background job, as
/// <c>EnqueueWelcomeEmailOnUserCreated</c> does - and anything that must happen atomically
/// with the change belongs in the same SaveChanges, not in an event handler. Only
/// SaveChangesAsync dispatches; the synchronous SaveChanges does not.
/// </remarks>
internal class EventDispatchInterceptor(IDomainEventDispatcher domainEventDispatcher)
    : SaveChangesInterceptor
{
    private readonly IDomainEventDispatcher _domainEventDispatcher = domainEventDispatcher;

    // Called after SaveChangesAsync has completed successfully
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = new CancellationToken()
    )
    {
        var context = eventData.Context;
        if (context is not ApplicationDatabaseContext appDbContext)
        {
            return await base.SavedChangesAsync(eventData, result, cancellationToken)
                .ConfigureAwait(false);
        }

        // Retrieve all tracked entities that have domain events
        var entitiesWithEvents = appDbContext
            .ChangeTracker.Entries<HasDomainEventsBase>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Any())
            .ToArray();

        // Dispatch and clear domain events
        await _domainEventDispatcher.DispatchAndClearEvents(entitiesWithEvents, cancellationToken);

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
