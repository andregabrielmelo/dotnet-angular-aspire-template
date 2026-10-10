namespace AppTemplate.Infrastructure.Idempotency;

/// <summary>
/// One idempotency key's outcome (table <c>idempotency_records</c>). There is no status column:
/// a row only becomes visible once it committed together with the command's changes, so a row
/// always means "done". Unique per (subject, operation, key).
/// </summary>
public sealed class IdempotencyRecord
{
    public Guid Id { get; init; }

    public required string Subject { get; init; }

    public required string Operation { get; init; }

    public required string Key { get; init; }

    /// <summary>SHA-256 (hex) of the operation and the request's semantic inputs.</summary>
    public required string Fingerprint { get; init; }

    /// <summary>The stored Result as JSON; set before commit.</summary>
    public string? ResultPayload { get; set; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset ExpiresAtUtc { get; init; }
}

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Subject).HasMaxLength(200);
        builder.Property(record => record.Operation).HasMaxLength(100);
        builder.Property(record => record.Key).HasMaxLength(IdempotencyOptions.MaxKeyLength);
        builder.Property(record => record.Fingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(record => record.ResultPayload).HasColumnType("jsonb");
        builder
            .HasIndex(record => new
            {
                record.Subject,
                record.Operation,
                record.Key,
            })
            .IsUnique();
        builder.HasIndex(record => record.ExpiresAtUtc);
    }
}

public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    public const int MaxKeyLength = 64;

    /// <summary>How long a key keeps replaying; then the cleanup job deletes it and the key is free.</summary>
    public TimeSpan RecordLifetime { get; set; } = TimeSpan.FromHours(24);
}
