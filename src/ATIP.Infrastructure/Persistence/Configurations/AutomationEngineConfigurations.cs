using ATIP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ATIP.Infrastructure.Persistence.Configurations;

public sealed class UiElementConfiguration : IEntityTypeConfiguration<UiElement>
{
    public void Configure(EntityTypeBuilder<UiElement> builder)
    {
        builder.ToTable("ui_elements");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Key).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Property(e => e.Platform).HasConversion<string>().HasMaxLength(40);
        builder.Property(e => e.ScreenUrlPattern).HasMaxLength(2000);
        builder.Property(e => e.Role).HasMaxLength(80);
        builder.Property(e => e.AccessibleName).HasMaxLength(500);

        // The key is how steps address an element, so it must be unambiguous within a project.
        // Filtered on IsDeleted so retiring an element frees its key for reuse.
        builder.HasIndex(e => new { e.ProjectId, e.Key })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasMany(e => e.Locators)
            .WithOne(l => l.Element)
            .HasForeignKey(l => l.ElementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UiElementLocatorConfiguration : IEntityTypeConfiguration<UiElementLocator>
{
    public void Configure(EntityTypeBuilder<UiElementLocator> builder)
    {
        builder.ToTable("ui_element_locators");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Strategy).HasConversion<string>().HasMaxLength(40);
        builder.Property(l => l.Origin).HasConversion<string>().HasMaxLength(40);
        builder.Property(l => l.Value).HasMaxLength(2000).IsRequired();

        // Resolution always reads "candidates for this element, best first"; this index serves
        // that query exactly so the hot path never sorts. Its ElementId prefix also covers the
        // duplicate check performed before a healed locator is inserted.
        //
        // Deliberately not unique on (ElementId, Strategy, Value): a long XPath can exceed
        // Postgres's btree entry limit, which would turn an oversized locator into an insert
        // error at the worst possible moment. Duplicates are prevented in the repository instead.
        builder.HasIndex(l => new { l.ElementId, l.IsQuarantined, l.Rank });
    }
}

public sealed class DataConnectionConfiguration : IEntityTypeConfiguration<DataConnection>
{
    public void Configure(EntityTypeBuilder<DataConnection> builder)
    {
        builder.ToTable("data_connections");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(120).IsRequired();
        builder.Property(c => c.Provider).HasConversion<string>().HasMaxLength(40);
        builder.Property(c => c.KeyId).HasMaxLength(100).IsRequired();

        builder.HasIndex(c => new { c.EnvironmentId, c.Name })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasOne<Domain.Entities.Environment>()
            .WithMany()
            .HasForeignKey(c => c.EnvironmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
