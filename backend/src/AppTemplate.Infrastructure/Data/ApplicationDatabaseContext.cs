using System.Reflection;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace AppTemplate.Infrastructure.Data;

public class ApplicationDatabaseContext(DbContextOptions<ApplicationDatabaseContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    // Named DomainUsers (not Users) because IdentityDbContext<ApplicationUser> already declares
    // its own DbSet<ApplicationUser> Users for Identity's credential records.
    public DbSet<User> DomainUsers => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Override OnModelCreating to apply class configurations from the assembly
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    // Override SaveChanges to ensure that all changes are saved asynchronously
    public override int SaveChanges() => SaveChangesAsync().GetAwaiter().GetResult();
}
