using ArtCommission.Domain.Entities.Commission;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations;

public class MilestoneConfiguration : IEntityTypeConfiguration<Milestone>
{
    public void Configure(EntityTypeBuilder<Milestone> builder)
    {
        builder.ToTable("Milestones");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.Price)
            .HasPrecision(18, 2);

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsConcurrencyToken();

        builder.Property(m => m.WipPreviewUrl)
            .HasMaxLength(500);

        builder.Property(m => m.WatermarkedUrl)
            .HasMaxLength(500);

        builder.Property(m => m.FinalDeliverableUrl)
            .HasMaxLength(500);
    }
}
