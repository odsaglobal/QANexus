using ATIP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ATIP.Infrastructure.Persistence.Configurations;

public sealed class ExplorationSessionConfiguration : IEntityTypeConfiguration<ExplorationSession>
{
    public void Configure(EntityTypeBuilder<ExplorationSession> builder)
    {
        builder.ToTable("exploration_sessions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(40);
        builder.Property(s => s.SeedUrl).HasMaxLength(2000);
        builder.Property(s => s.ErrorMessage).HasMaxLength(2000);

        builder.HasIndex(s => s.ProjectId);
        builder.HasIndex(s => s.ScenarioId);
        builder.HasIndex(s => s.SuiteId);

        builder.HasMany(s => s.Pages)
            .WithOne(p => p.Session)
            .HasForeignKey(p => p.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Environment)
            .WithMany()
            .HasForeignKey(s => s.EnvironmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DiscoveredPageConfiguration : IEntityTypeConfiguration<DiscoveredPage>
{
    public void Configure(EntityTypeBuilder<DiscoveredPage> builder)
    {
        builder.ToTable("discovered_pages");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Url).HasMaxLength(2000).IsRequired();
        builder.Property(p => p.Path).HasMaxLength(2000).IsRequired();
        builder.Property(p => p.Title).HasMaxLength(400);
        builder.Property(p => p.ScreenshotPath).HasMaxLength(1024);
        builder.Property(p => p.DomSnapshotPath).HasMaxLength(1024);
        builder.Property(p => p.DiscoveredFromUrl).HasMaxLength(2000);
        builder.Property(p => p.AccessibilityTreeJson).HasColumnType("text");

        builder.HasIndex(p => p.SessionId);
        builder.HasIndex(p => p.ProjectId);
    }
}

public sealed class ScenarioStepResultConfiguration : IEntityTypeConfiguration<ScenarioStepResult>
{
    public void Configure(EntityTypeBuilder<ScenarioStepResult> builder)
    {
        builder.ToTable("scenario_step_results");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(40);
        builder.Property(r => r.Action).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.Detail).HasMaxLength(2000);
        builder.Property(r => r.ChecksJson).HasColumnType("jsonb");
        builder.Property(r => r.Url).HasMaxLength(2000);
        builder.Property(r => r.ScreenshotPath).HasMaxLength(500);

        builder.HasIndex(r => r.SessionId);
        builder.HasIndex(r => r.ScenarioId);
    }
}
