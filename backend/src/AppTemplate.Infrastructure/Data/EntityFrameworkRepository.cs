using Ardalis.Specification.EntityFrameworkCore;

namespace AppTemplate.Infrastructure.Data;

/// <summary>
/// The Ardalis.Specification repository, plus one translation: EF Core's concurrency exception
/// becomes <see cref="ConcurrencyConflictException"/>, so use cases never see EF Core types.
/// </summary>
public class EntityFrameworkRepository<T>(ApplicationDatabaseContext dbContext)
    : RepositoryBase<T>(dbContext),
        IRepository<T>
    where T : class, IAggregateRoot
{
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(exception);
        }
    }
}
