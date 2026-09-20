using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.Infrastructure.Data.Configurations;

public class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        builder.Property(entity => entity.UserId).HasVogenConversion().IsRequired();

        builder.Property(entity => entity.Provider).HasMaxLength(50).IsRequired();

        builder.Property(entity => entity.ProviderKey).HasMaxLength(256).IsRequired();

        builder.HasIndex(entity => new { entity.Provider, entity.ProviderKey }).IsUnique();
    }
}
