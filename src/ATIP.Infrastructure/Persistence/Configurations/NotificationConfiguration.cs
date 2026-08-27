using ATIP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ATIP.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(1000).IsRequired();
        builder.Property(n => n.Level).HasMaxLength(20).IsRequired();
        builder.Property(n => n.Category).HasMaxLength(40).IsRequired();
        builder.Property(n => n.EntityType).HasMaxLength(80);

        builder.HasIndex(n => new { n.TenantId, n.IsRead, n.CreatedAtUtc });
    }
}
