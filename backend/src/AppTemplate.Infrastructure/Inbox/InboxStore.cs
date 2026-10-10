using AppTemplate.Infrastructure.Data;

namespace AppTemplate.Infrastructure.Inbox;

/// <summary>
/// The inbox (idempotent consumer): records which consumer has handled which message, so a
/// redelivered message skips consumers that already completed. It works on the
/// <b>caller's</b> <see cref="ApplicationDatabaseContext"/>, never its own: the inbox row has
/// to commit in the same transaction as the consumer's changes. Commit both, and a redelivery
/// is skipped; roll both back, and the retry runs the consumer again.
/// </summary>
public sealed class InboxStore(TimeProvider timeProvider)
{
    public Task<bool> HasProcessedAsync(
        ApplicationDatabaseContext context,
        Guid messageId,
        string consumer,
        CancellationToken cancellationToken
    ) =>
        context.InboxMessages.AnyAsync(
            inbox => inbox.MessageId == messageId && inbox.Consumer == consumer,
            cancellationToken
        );

    /// <summary>Adds the record to <paramref name="context"/>; the caller's save and commit persist it.</summary>
    public void MarkProcessed(
        ApplicationDatabaseContext context,
        Guid messageId,
        string consumer
    ) =>
        context.InboxMessages.Add(
            new InboxMessage
            {
                MessageId = messageId,
                Consumer = consumer,
                ProcessedAtUtc = timeProvider.GetUtcNow(),
            }
        );
}
