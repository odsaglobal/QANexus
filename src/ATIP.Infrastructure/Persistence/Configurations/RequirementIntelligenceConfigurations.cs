using ATIP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ATIP.Infrastructure.Persistence.Configurations;

public sealed class RequirementConfiguration : IEntityTypeConfiguration<Requirement>
{
    public void Configure(EntityTypeBuilder<Requirement> builder)
    {
        builder.ToTable("requirements");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.SourceType).HasConversion<string>().HasMaxLength(40);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(40);
        builder.Property(r => r.StoragePath).HasMaxLength(1024);
        builder.Property(r => r.ExtractedText).HasColumnType("text");
        builder.Property(r => r.ErrorMessage).HasMaxLength(2000);

        builder.HasIndex(r => r.ProjectId);

        builder.HasMany(r => r.Modules)
            .WithOne(m => m.Requirement)
            .HasForeignKey(m => m.RequirementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RequirementModuleConfiguration : IEntityTypeConfiguration<RequirementModule>
{
    public void Configure(EntityTypeBuilder<RequirementModule> builder)
    {
        builder.ToTable("requirement_modules");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Description).HasMaxLength(2000);

        builder.HasIndex(m => m.RequirementId);
        builder.HasIndex(m => m.ProjectId);

        builder.HasMany(m => m.Features)
            .WithOne(f => f.Module)
            .HasForeignKey(f => f.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class FeatureConfiguration : IEntityTypeConfiguration<Feature>
{
    public void Configure(EntityTypeBuilder<Feature> builder)
    {
        builder.ToTable("features");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name).HasMaxLength(200).IsRequired();
        builder.Property(f => f.Description).HasMaxLength(4000);
        builder.Property(f => f.Priority).HasConversion<string>().HasMaxLength(40);
        builder.Property(f => f.BusinessRulesJson).HasColumnType("jsonb");

        builder.HasIndex(f => f.ModuleId);
        builder.HasIndex(f => f.ProjectId);

        builder.HasMany(f => f.UserStories)
            .WithOne(s => s.Feature)
            .HasForeignKey(s => s.FeatureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(f => f.Scenarios)
            .WithOne(s => s.Feature)
            .HasForeignKey(s => s.FeatureId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserStoryConfiguration : IEntityTypeConfiguration<UserStory>
{
    public void Configure(EntityTypeBuilder<UserStory> builder)
    {
        builder.ToTable("user_stories");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.AsA).HasMaxLength(200).IsRequired();
        builder.Property(s => s.IWant).HasMaxLength(500).IsRequired();
        builder.Property(s => s.SoThat).HasMaxLength(500);
        builder.Property(s => s.AcceptanceCriteriaJson).HasColumnType("jsonb");

        builder.HasIndex(s => s.FeatureId);
    }
}

public sealed class ScenarioConfiguration : IEntityTypeConfiguration<Scenario>
{
    public void Configure(EntityTypeBuilder<Scenario> builder)
    {
        builder.ToTable("scenarios");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title).HasMaxLength(300).IsRequired();
        builder.Property(s => s.Type).HasConversion<string>().HasMaxLength(40);
        builder.Property(s => s.Priority).HasConversion<string>().HasMaxLength(40);
        builder.Property(s => s.Risk).HasConversion<string>().HasMaxLength(40);
        builder.Property(s => s.Source).HasConversion<string>().HasMaxLength(40);
        builder.Property(s => s.Preconditions).HasMaxLength(2000);
        builder.Property(s => s.ExpectedResult).HasMaxLength(2000);
        builder.Property(s => s.JiraKey).HasMaxLength(60);
        builder.Property(s => s.TagsJson).HasColumnType("jsonb");
        builder.Property(s => s.ProposedStepsJson).HasColumnType("jsonb");
        builder.Property(s => s.PreviousStepsJson).HasColumnType("jsonb");

        builder.HasIndex(s => s.FeatureId);
        builder.HasIndex(s => s.ProjectId);

        builder.HasMany(s => s.Steps)
            .WithOne(st => st.Scenario)
            .HasForeignKey(st => st.ScenarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ScenarioStepConfiguration : IEntityTypeConfiguration<ScenarioStep>
{
    public void Configure(EntityTypeBuilder<ScenarioStep> builder)
    {
        builder.ToTable("scenario_steps");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Action).HasMaxLength(1000).IsRequired();
        builder.Property(s => s.ExpectedResult).HasMaxLength(1000);
        builder.Property(s => s.RecordedActionsJson).HasColumnType("jsonb");
        builder.Property(s => s.ReviewReason).HasMaxLength(1000);

        builder.HasIndex(s => s.ScenarioId);
    }
}
