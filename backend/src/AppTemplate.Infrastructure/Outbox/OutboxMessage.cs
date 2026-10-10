namespace AppTemplate.Infrastructure.Outbox;

/// <summary>
/// One integration event waiting to be delivered (table <c>outbox_messages</c>). Written in the
/// same transaction as the change that raised it; the relay sets <see cref="ProcessedAtUtc"/>
/// once every handler has run, or <see cref="DeadLetteredAtUtc"/> once it gives up.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }

    /// <summary>The registered contract key, such as <c>user.provisioned.v1</c>; never a CLR type name.</summary>
    public required string Type { get; init; }

    /// <summary>The event as JSON.</summary>
    public required string Payload { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; }

    public DateTimeOffset? ProcessedAtUtc { get; set; }

    public int Attempts { get; set; }

    /// <summary>Not claimed before this time: the retry back-off.</summary>
    public DateTimeOffset NextAttemptAtUtc { get; set; }

    /// <summary>
    /// A worker's claim. Until this time no other worker takes the message; after it, the
    /// message is claimable again (the worker is assumed dead).
    /// </summary>
    public DateTimeOffset? LockedUntilUtc { get; set; }

    public string? LastError { get; set; }

    /// <summary>Set when the relay gives up; the row stays for inspection and requeueing.</summary>
    public DateTimeOffset? DeadLetteredAtUtc { get; set; }
}

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
