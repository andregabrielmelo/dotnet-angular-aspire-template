namespace AppTemplate.Infrastructure.Inbox;

/// <summary>
/// A handler finished an outbox message (table <c>inbox_messages</c>, keyed by message and
/// handler). Written in the same transaction as the handler's own changes, so a redelivered
/// message skips handlers that already completed.
/// </summary>
public sealed class InboxMessage
{
    public Guid MessageId { get; init; }

    /// <summary>The handler's full type name.</summary>
    public required string Consumer { get; init; }

    public DateTimeOffset ProcessedAtUtc { get; init; }
}
