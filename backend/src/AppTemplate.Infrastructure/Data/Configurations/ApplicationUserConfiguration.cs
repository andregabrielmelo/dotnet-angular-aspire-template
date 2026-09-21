using AppTemplate.Infrastructure.Identity;

namespace AppTemplate.Infrastructure.Data.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.DomainUserId).HasVogenConversion().IsRequired();

        builder.HasIndex(u => u.DomainUserId).IsUnique();
    }
}
