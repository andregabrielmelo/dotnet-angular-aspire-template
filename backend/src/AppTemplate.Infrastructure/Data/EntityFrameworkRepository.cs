using Ardalis.Specification.EntityFrameworkCore;
using Npgsql;

namespace AppTemplate.Infrastructure.Data;

// inherit from Ardalis.Specification type
public class EntityFrameworkRepository<T>(ApplicationDatabaseContext dbContext)
    : RepositoryBase<T>(dbContext),
        IRepository<T>
    where T : class, IAggregateRoot
{
    /// <summary>
    /// Every write (Add/Update/Delete) saves through here. A unique-index violation is turned
    /// into <see cref="UniqueConstraintViolationException"/> so use cases can handle races
    /// (e.g. two requests provisioning the same user) instead of failing with a 500.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException
                    is PostgresException
                    {
                        SqlState: PostgresErrorCodes.UniqueViolation
                    } postgresException
            )
        {
            // Stop tracking the rejected changes, or the next save on this (scoped) context
            // would try to write them again.
            foreach (var entry in ex.Entries)
            {
                entry.State = EntityState.Detached;
            }

            throw new UniqueConstraintViolationException(postgresException.ConstraintName, ex);
        }
    }
}
