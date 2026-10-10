namespace AppTemplate.UseCases.Idempotency;

/// <summary>
/// A command a client may safely send more than once with the same <c>Idempotency-Key</c>: it
/// runs once, and every repeat gets the first result back (see <see cref="IdempotencyBehavior{TMessage, TResponse}"/>).
/// </summary>
public interface IIdempotentCommand
{
    /// <summary>The client's key; null runs the command normally.</summary>
    string? IdempotencyKey { get; }

    /// <summary>A stable name for what the command does, such as <c>users.update.v1</c>. Never the route.</summary>
    string Operation { get; }
}

/// <summary>
/// The command's own authorization, run <b>before</b> a stored result is replayed, so a repeat
/// from a caller who has since lost access is refused rather than answered from the store.
/// </summary>
public interface ICommandAuthorizer<in TCommand>
{
    /// <returns>False to refuse with 403. A missing resource should return true and let the handler answer 404.</returns>
    ValueTask<bool> IsAllowedAsync(TCommand command, CancellationToken cancellationToken);
}

/// <summary>A database transaction around a use case (implemented in Infrastructure).</summary>
public interface IUnitOfWork
{
    bool InTransaction { get; }

    Task<IUnitOfWorkTransaction> BeginAsync(CancellationToken cancellationToken);

    /// <summary>Runs once the current transaction commits (immediately if there is none); dropped on rollback.</summary>
    void AfterCommit(Func<CancellationToken, ValueTask> action);
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);

    Task RollbackAsync(CancellationToken cancellationToken);
}

/// <summary>Durable idempotency records (table <c>idempotency_records</c>, implemented in Infrastructure).</summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Inserts the record inside the current transaction. A concurrent duplicate's insert waits
    /// on the unique index until this transaction ends. False when a committed record exists.
    /// </summary>
    Task<bool> TryClaimAsync(
        IdempotencyScope scope,
        string fingerprint,
        CancellationToken cancellationToken
    );

    /// <summary>Stores the result on the claimed record, inside the same transaction.</summary>
    Task CompleteAsync(string resultPayload, CancellationToken cancellationToken);

    /// <summary>A committed record, read outside any transaction.</summary>
    Task<StoredIdempotencyRecord?> FindAsync(
        IdempotencyScope scope,
        CancellationToken cancellationToken
    );
}

/// <summary>Keys are scoped per caller and operation: another subject's identical key is unrelated.</summary>
public sealed record IdempotencyScope(string Subject, string Operation, string Key);

public sealed record StoredIdempotencyRecord(string Fingerprint, string? ResultPayload);

public static class IdempotencyResults
{
    /// <summary>A key reused with different inputs: a <c>Conflict</c> mapped to 422.</summary>
    public const string KeyReused =
        "This Idempotency-Key was already used for a different request. Use a new key.";
}
