using ArtCommission.Domain.Entities.Commission;
using ArtCommission.Domain.Entities.ArtistStudio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations;

public class CreatorAddonConfiguration : IEntityTypeConfiguration<CreatorAddon>
{
    public void Configure(EntityTypeBuilder<CreatorAddon> b)
    {
        b.ToTable("CreatorAddons");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Price).HasPrecision(18, 2);
        b.Property(x => x.AllowedMilestone).HasMaxLength(20).IsRequired();
        b.Property(x => x.PresetKey).HasMaxLength(60);
        b.HasOne<CreatorProfile>().WithMany().HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.CreatorId, x.IsActive });
    }
}

public class CommissionAddonConfiguration : IEntityTypeConfiguration<CommissionAddon>
{
    public void Configure(EntityTypeBuilder<CommissionAddon> b)
    {
        b.ToTable("CommissionAddons");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Price).HasPrecision(18, 2);
        b.Property(x => x.AllowedMilestone).HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired().IsConcurrencyToken();
        b.HasOne<Commission>().WithMany().HasForeignKey(x => x.CommissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.CommissionId, x.CreatedAt });
    }
}
