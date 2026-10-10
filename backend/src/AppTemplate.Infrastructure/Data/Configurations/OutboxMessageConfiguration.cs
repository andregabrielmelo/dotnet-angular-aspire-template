using AppTemplate.Infrastructure.Outbox;

namespace AppTemplate.Infrastructure.Data.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Type).HasMaxLength(200).IsRequired();
        builder.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(message => message.LastError).HasMaxLength(2000);

        // What the relay scans for: pending messages, oldest first.
        builder
            .HasIndex(message => new { message.NextAttemptAtUtc, message.OccurredAtUtc })
            .HasFilter("processed_at_utc IS NULL AND dead_lettered_at_utc IS NULL")
            .HasDatabaseName("ix_outbox_messages_pending");
    }
}
