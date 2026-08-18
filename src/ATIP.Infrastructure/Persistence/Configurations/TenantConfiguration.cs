using ATIP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ATIP.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Slug).HasMaxLength(80).IsRequired();
        builder.Property(t => t.ExternalDirectoryId).HasMaxLength(200);

        builder.HasIndex(t => t.Slug).IsUnique();

        builder.HasMany(t => t.Projects)
            .WithOne(p => p.Tenant)
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Users)
            .WithOne(u => u.Tenant)
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
