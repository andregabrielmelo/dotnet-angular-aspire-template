using AppTemplate.Infrastructure.Data;
using AppTemplate.UseCases.Idempotency;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AppTemplate.Infrastructure.Idempotency;

internal sealed class EfIdempotencyStore(
    ApplicationDatabaseContext context,
    IOptions<IdempotencyOptions> options,
    TimeProvider timeProvider
) : IIdempotencyStore
{
    private IdempotencyRecord? _claimed;

    public async Task<bool> TryClaimAsync(
        IdempotencyScope scope,
        string fingerprint,
        CancellationToken cancellationToken
    )
    {
        var now = timeProvider.GetUtcNow();
        var record = new IdempotencyRecord
        {
            Id = Guid.CreateVersion7(),
            Subject = scope.Subject,
            Operation = scope.Operation,
            Key = scope.Key,
            Fingerprint = fingerprint,
            CreatedAtUtc = now,
            ExpiresAtUtc = now + options.Value.RecordLifetime,
        };
        context.IdempotencyRecords.Add(record);
        try
        {
            // Blocks while another transaction holds an uncommitted row with the same key.
            await context.SaveChangesAsync(cancellationToken);
            _claimed = record;
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException
                    is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
            )
        {
            context.Entry(record).State = EntityState.Detached;
            return false;
        }
    }

    public async Task CompleteAsync(string resultPayload, CancellationToken cancellationToken)
    {
        var record =
            _claimed
            ?? throw new InvalidOperationException("Complete called without a successful claim.");
        record.ResultPayload = resultPayload;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<StoredIdempotencyRecord?> FindAsync(
        IdempotencyScope scope,
        CancellationToken cancellationToken
    ) =>
        await context
            .IdempotencyRecords.AsNoTracking()
            .Where(record =>
                record.Subject == scope.Subject
                && record.Operation == scope.Operation
                && record.Key == scope.Key
            )
            .Select(record => new StoredIdempotencyRecord(record.Fingerprint, record.ResultPayload))
            .SingleOrDefaultAsync(cancellationToken);
}
