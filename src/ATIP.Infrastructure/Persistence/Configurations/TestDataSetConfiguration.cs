using ATIP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ATIP.Infrastructure.Persistence.Configurations;

public sealed class TestDataSetConfiguration : IEntityTypeConfiguration<TestDataSet>
{
    public void Configure(EntityTypeBuilder<TestDataSet> builder)
    {
        builder.ToTable("test_data_sets");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name).HasMaxLength(120).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(1000);
        builder.Property(d => d.ColumnsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(d => d.RowsJson).HasColumnType("jsonb").IsRequired();

        builder.HasOne(d => d.Environment)
            .WithMany()
            .HasForeignKey(d => d.EnvironmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.EnvironmentId);
        builder.HasIndex(d => new { d.EnvironmentId, d.Name }).IsUnique();
    }
}
