using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder
            .Property(entity => entity.Id)
            .HasValueGenerator<VogenIdValueGenerator<ApplicationDatabaseContext, User, UserId>>()
            .HasVogenConversion()
            .IsRequired();

        builder
            .Property(entity => entity.Name)
            .HasVogenConversion()
            .HasMaxLength(UserName.MaxLength)
            .IsRequired();

        builder
            .Property(entity => entity.Email)
            .HasConversion(email => email.Value, value => new EmailAddress(value))
            .HasMaxLength(320)
            .IsRequired();

        builder.HasIndex(entity => entity.Email).IsUnique();

        builder.Property(entity => entity.Password);

        builder.Property(entity => entity.EmailConfirmed).IsRequired();

        builder.OwnsOne(builder => builder.PhoneNumber);

        builder
            .HasMany(entity => entity.ExternalLogins)
            .WithOne()
            .HasForeignKey(login => login.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .Navigation(entity => entity.ExternalLogins)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder
            .HasMany(entity => entity.Tokens)
            .WithOne()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(entity => entity.Tokens).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder
            .HasMany(entity => entity.RefreshTokens)
            .WithOne()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .Navigation(entity => entity.RefreshTokens)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
