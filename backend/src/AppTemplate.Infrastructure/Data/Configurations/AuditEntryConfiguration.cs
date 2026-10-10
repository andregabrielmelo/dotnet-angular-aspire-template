using AppTemplate.Infrastructure.Auditing;

namespace AppTemplate.Infrastructure.Data.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Actor).HasMaxLength(200);
        builder.Property(entry => entry.Action).HasMaxLength(100);
        builder.Property(entry => entry.EntityType).HasMaxLength(100);
        builder.Property(entry => entry.EntityKey).HasMaxLength(200);
        builder.Property(entry => entry.Outcome).HasMaxLength(20);
        builder.Property(entry => entry.Changes).HasColumnType("jsonb");
        builder.Property(entry => entry.TraceId).HasMaxLength(64);

        // The query endpoint filters only on these, always with a time range.
        builder.HasIndex(entry => entry.OccurredAtUtc);
        builder.HasIndex(entry => new
        {
            entry.EntityType,
            entry.EntityKey,
            entry.OccurredAtUtc,
        });
        builder.HasIndex(entry => new { entry.Actor, entry.OccurredAtUtc });
    }
}
