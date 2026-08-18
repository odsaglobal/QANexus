using ATIP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ATIP.Infrastructure.Persistence.Configurations;

public sealed class CredentialConfiguration : IEntityTypeConfiguration<Credential>
{
    public void Configure(EntityTypeBuilder<Credential> builder)
    {
        builder.ToTable("credentials");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(120).IsRequired();
        builder.Property(c => c.Username).HasMaxLength(320).IsRequired();
        builder.Property(c => c.EncryptedSecret).IsRequired();
        builder.Property(c => c.EncryptionNonce).IsRequired();
        builder.Property(c => c.KeyId).HasMaxLength(80).IsRequired();

        builder.HasIndex(c => c.ProjectId);
    }
}
