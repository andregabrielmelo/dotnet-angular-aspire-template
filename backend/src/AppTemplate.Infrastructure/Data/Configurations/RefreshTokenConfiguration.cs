using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Property(entity => entity.UserId).HasVogenConversion().IsRequired();

        builder.Property(entity => entity.TokenHash).HasMaxLength(128).IsRequired();

        builder.Property(entity => entity.ReplacedByTokenHash).HasMaxLength(128);

        builder.HasIndex(entity => entity.TokenHash);
    }
}
