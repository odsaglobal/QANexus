using ATIP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ATIP.Infrastructure.Persistence.Configurations;

public sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Category).HasMaxLength(60).IsRequired();
        builder.Property(a => a.Summary).HasMaxLength(1000).IsRequired();
        builder.Property(a => a.UserEmail).HasMaxLength(320);
        builder.Property(a => a.EntityType).HasMaxLength(80);
        builder.Property(a => a.IpAddress).HasMaxLength(64);

        builder.HasIndex(a => new { a.TenantId, a.TimestampUtc });
        builder.HasIndex(a => a.EntityId);
    }
}
