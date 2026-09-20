using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.Infrastructure.Data.Configurations;

public class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> builder)
    {
        builder.Property(entity => entity.UserId).HasVogenConversion().IsRequired();

        builder
            .Property(entity => entity.Purpose)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(entity => entity.TokenHash).HasMaxLength(128).IsRequired();

        builder.HasIndex(entity => entity.TokenHash);
    }
}
