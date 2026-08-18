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

        builder.HasMany(p => p.Elements)
            .WithOne(e => e.Page)
            .HasForeignKey(e => e.PageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DiscoveredElementConfiguration : IEntityTypeConfiguration<DiscoveredElement>
{
    public void Configure(EntityTypeBuilder<DiscoveredElement> builder)
    {
        builder.ToTable("discovered_elements");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).HasMaxLength(300);
        builder.Property(e => e.Role).HasMaxLength(80);
        builder.Property(e => e.AriaLabel).HasMaxLength(300);
        builder.Property(e => e.TextContent).HasMaxLength(500);
        builder.Property(e => e.Placeholder).HasMaxLength(200);
        builder.Property(e => e.DataTestId).HasMaxLength(200);
        builder.Property(e => e.DomPath).HasMaxLength(1000);
        builder.Property(e => e.ScreenshotPath).HasMaxLength(1024);
        builder.Property(e => e.AiDescription).HasMaxLength(500);
        builder.Property(e => e.NearbyLabelsJson).HasColumnType("jsonb");
        builder.Property(e => e.BoundingBoxJson).HasColumnType("jsonb");

        builder.HasIndex(e => e.PageId);
        builder.HasIndex(e => e.ProjectId);

        builder.HasMany(e => e.Locators)
            .WithOne(l => l.Element)
            .HasForeignKey(l => l.ElementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ElementLocatorConfiguration : IEntityTypeConfiguration<ElementLocator>
{
    public void Configure(EntityTypeBuilder<ElementLocator> builder)
    {
        builder.ToTable("element_locators");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Strategy).HasConversion<string>().HasMaxLength(40);
        builder.Property(l => l.Value).HasMaxLength(2000).IsRequired();

        builder.HasIndex(l => l.ElementId);
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
        builder.Property(r => r.Url).HasMaxLength(2000);

        builder.HasIndex(r => r.SessionId);
        builder.HasIndex(r => r.ScenarioId);
    }
}
