using ArtCommission.Domain.Entities.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Payment;

public class PlatformConfigConfiguration : IEntityTypeConfiguration<PlatformConfig>
{
    public void Configure(EntityTypeBuilder<PlatformConfig> builder)
    {
        builder.ToTable("PlatformConfig");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Key)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Value)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasMaxLength(500);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.IsDeleted)
            .HasDefaultValue(false);

        builder.HasIndex(c => c.Key)
            .IsUnique()
            .HasDatabaseName("UX_PlatformConfig_Key");
    }
}
