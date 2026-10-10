namespace AppTemplate.Infrastructure.Auditing;

/// <summary>One audited action (table <c>audit_entries</c>). Append-only: nothing updates a row.</summary>
public sealed class AuditEntry
{
    /// <summary>What a personal or secret value is recorded as.</summary>
    public const string Masked = "[redacted]";

    public Guid Id { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary><c>user:{sub}</c>, <c>system:{job id}</c> or <c>anonymous</c>.</summary>
    public required string Actor { get; init; }

    /// <summary>See <c>AuditActions</c>: <c>created</c>, <c>job.paused</c>, ...</summary>
    public required string Action { get; init; }

    public required string EntityType { get; init; }

    public required string EntityKey { get; init; }

    /// <summary><c>succeeded</c>, <c>failed</c> or <c>denied</c>.</summary>
    public required string Outcome { get; init; }

    /// <summary>JSON: allowlisted property name to { old, new }; personal data masked.</summary>
    public string? Changes { get; init; }

    public string? TraceId { get; init; }
}

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
