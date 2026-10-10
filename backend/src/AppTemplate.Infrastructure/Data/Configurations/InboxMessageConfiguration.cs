using AppTemplate.Infrastructure.Inbox;

namespace AppTemplate.Infrastructure.Data.Configurations;

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");
        builder.HasKey(message => new { message.MessageId, message.Consumer });
        builder.Property(message => message.Consumer).HasMaxLength(300);
    }
}
