namespace AppTemplate.Core.Interfaces;

/// <summary>
/// Operator actions on the transactional outbox (implemented in Infrastructure).
/// Implemented by <c>OutboxAdministration</c> (Postgres). Used by <c>RequeueDeadLetteredMessagesHandler</c>.
/// </summary>
public interface IOutboxAdministration
{
    /// <summary>Makes every dead-lettered message due again with a fresh attempt count; returns how many.</summary>
    Task<int> RequeueDeadLetteredAsync(CancellationToken cancellationToken);
}
