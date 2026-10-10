using AppTemplate.Infrastructure.Data;
using AppTemplate.UseCases.Idempotency;
using Microsoft.EntityFrameworkCore.Storage;

namespace AppTemplate.Infrastructure.Idempotency;

/// <summary>
/// Transactions on the scope's DbContext, so repositories and stores used by the same use case
/// share them. Work deferred with <see cref="AfterCommit"/> (cache invalidation) runs only once
/// the data is committed: invalidating earlier would let a concurrent read re-cache the old row.
/// </summary>
internal sealed partial class EfUnitOfWork(
    ApplicationDatabaseContext context,
    ILogger<EfUnitOfWork> logger
) : IUnitOfWork
{
    private readonly List<Func<CancellationToken, ValueTask>> _afterCommit = [];
    private readonly ApplicationDatabaseContext _context = context;

    public bool InTransaction => _context.Database.CurrentTransaction is not null;

    public async Task<IUnitOfWorkTransaction> BeginAsync(CancellationToken cancellationToken) =>
        new Transaction(this, await _context.Database.BeginTransactionAsync(cancellationToken));

    public void AfterCommit(Func<CancellationToken, ValueTask> action)
    {
        if (InTransaction)
        {
            _afterCommit.Add(action);
        }
        else
        {
            action(CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }
    }

    private async Task RunAfterCommitAsync(CancellationToken cancellationToken)
    {
        var actions = _afterCommit.ToList();
        _afterCommit.Clear();
        foreach (var action in actions)
        {
            try
            {
                await action(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The data is committed; a follow-up failing must not turn that into an error.
                LogAfterCommitFailed(logger, exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "An action deferred until commit failed")]
    private static partial void LogAfterCommitFailed(ILogger logger, Exception exception);

    private sealed class Transaction(EfUnitOfWork owner, IDbContextTransaction transaction)
        : IUnitOfWorkTransaction
    {
        private bool _completed;

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            await transaction.CommitAsync(cancellationToken);
            _completed = true;
            await owner.RunAfterCommitAsync(cancellationToken);
        }

        public async Task RollbackAsync(CancellationToken cancellationToken)
        {
            if (_completed)
            {
                return;
            }
            _completed = true;
            owner._afterCommit.Clear();
            await transaction.RollbackAsync(cancellationToken);
            // Rolled back changes must not be retried by a later SaveChanges in this scope.
            owner._context.ChangeTracker.Clear();
        }

        public async ValueTask DisposeAsync()
        {
            if (!_completed)
            {
                await RollbackAsync(CancellationToken.None);
            }
            await transaction.DisposeAsync();
        }
    }
}
