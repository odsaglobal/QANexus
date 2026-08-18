using ATIP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ATIP.Infrastructure.Persistence.Configurations;

public sealed class TestSuiteConfiguration : IEntityTypeConfiguration<TestSuite>
{
    public void Configure(EntityTypeBuilder<TestSuite> builder)
    {
        builder.ToTable("test_suites");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(2000);

        builder.HasIndex(s => s.ProjectId);

        builder.HasMany(s => s.Scenarios)
            .WithOne(ss => ss.Suite)
            .HasForeignKey(ss => ss.SuiteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TestSuiteScenarioConfiguration : IEntityTypeConfiguration<TestSuiteScenario>
{
    public void Configure(EntityTypeBuilder<TestSuiteScenario> builder)
    {
        builder.ToTable("test_suite_scenarios");
        builder.HasKey(s => s.Id);

        // A scenario appears at most once in a given suite.
        builder.HasIndex(s => new { s.SuiteId, s.ScenarioId }).IsUnique();

        builder.HasOne(s => s.Scenario)
            .WithMany()
            .HasForeignKey(s => s.ScenarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
