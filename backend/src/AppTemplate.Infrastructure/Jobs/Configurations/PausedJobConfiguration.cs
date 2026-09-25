using AppTemplate.Infrastructure.Jobs.Models;

namespace AppTemplate.Infrastructure.Jobs.Configurations;

/// <summary>
/// Stored next to Hangfire's own tables, in the <c>hangfire</c> schema. EF's schema creation is
/// idempotent, so it coexists with Hangfire.PostgreSql creating that schema too.
/// </summary>
public class PausedJobConfiguration : IEntityTypeConfiguration<PausedJob>
{
    public const int MaxLength = 100;

    public void Configure(EntityTypeBuilder<PausedJob> builder)
    {
        builder.ToTable("paused_jobs", "hangfire");

        builder.HasKey(job => job.Id);

        builder.Property(job => job.JobId).HasMaxLength(MaxLength).IsRequired();
        builder.HasIndex(job => job.JobId).IsUnique();

        builder.Property(job => job.OriginalCron).HasMaxLength(MaxLength).IsRequired();

        builder.Property(job => job.PausedAtUtc).IsRequired();
    }
}
