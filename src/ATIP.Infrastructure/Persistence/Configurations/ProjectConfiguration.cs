using ATIP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ATIP.Infrastructure.Persistence.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Key).HasMaxLength(60).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(40);

        // Project key is unique within a tenant.
        builder.HasIndex(p => new { p.TenantId, p.Key }).IsUnique();

        builder.HasMany(p => p.Environments)
            .WithOne(e => e.Project)
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Members)
            .WithOne(m => m.Project)
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Credentials)
            .WithOne(c => c.Project)
            .HasForeignKey(c => c.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.TestDataSets)
            .WithOne(d => d.Project)
            .HasForeignKey(d => d.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
