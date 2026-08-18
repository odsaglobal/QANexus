using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EnvEntity = ATIP.Domain.Entities.Environment;

namespace ATIP.Infrastructure.Persistence.Configurations;

public sealed class EnvironmentConfiguration : IEntityTypeConfiguration<EnvEntity>
{
    public void Configure(EntityTypeBuilder<EnvEntity> builder)
    {
        builder.ToTable("environments");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(40);
        builder.Property(e => e.BaseUrl).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.VariablesJson).HasColumnType("jsonb");

        builder.HasIndex(e => new { e.ProjectId, e.Name }).IsUnique();

        builder.HasMany(e => e.BrowserProfiles)
            .WithOne(b => b.Environment)
            .HasForeignKey(b => b.EnvironmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
