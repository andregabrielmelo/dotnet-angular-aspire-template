using System.Reflection;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Infrastructure.Auditing;
using AppTemplate.Infrastructure.Outbox;

namespace AppTemplate.Infrastructure.Data;

public class ApplicationDatabaseContext(DbContextOptions<ApplicationDatabaseContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    // Override OnModelCreating to apply class configurations from the assembly
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    // Override SaveChanges to ensure that all changes are saved asynchronously
    public override int SaveChanges() => SaveChangesAsync().GetAwaiter().GetResult();
}
