using ATIP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ATIP.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(200);
        builder.Property(u => u.ExternalObjectId).HasMaxLength(200);
        builder.Property(u => u.SystemRole).HasConversion<string>().HasMaxLength(40);

        // Email is globally unique so that login (which precedes tenant resolution) is unambiguous.
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.TenantId);
    }
}
