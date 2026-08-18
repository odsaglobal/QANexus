using ATIP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ATIP.Infrastructure.Persistence.Configurations;

public sealed class BrowserProfileConfiguration : IEntityTypeConfiguration<BrowserProfile>
{
    public void Configure(EntityTypeBuilder<BrowserProfile> builder)
    {
        builder.ToTable("browser_profiles");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name).HasMaxLength(120).IsRequired();
        builder.Property(b => b.Browser).HasConversion<string>().HasMaxLength(40);
        builder.Property(b => b.Locale).HasMaxLength(20);
        builder.Property(b => b.TimezoneId).HasMaxLength(80);
        builder.Property(b => b.DeviceName).HasMaxLength(80);
        builder.Property(b => b.UserAgent).HasMaxLength(500);

        builder.HasIndex(b => b.EnvironmentId);
    }
}
