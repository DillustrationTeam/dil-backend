using ArtCommission.Domain.Entities.Ai;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Ai;

public class AiReferenceGenerationConfiguration : IEntityTypeConfiguration<AiReferenceGeneration>
{
    public void Configure(EntityTypeBuilder<AiReferenceGeneration> builder)
    {
        builder.ToTable("AiReferenceGenerations");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.BriefText).IsRequired();
        builder.Property(g => g.LayoutHint).HasMaxLength(300);
        builder.Property(g => g.Model).HasMaxLength(80).IsRequired();
        builder.Property(g => g.BlockReason).HasMaxLength(500);
        builder.Property(g => g.Error).HasMaxLength(1000);

        builder.Property(g => g.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(AiReferenceGenerationStatus.Pending)
            .IsRequired();

        builder.Property(g => g.ProgressPercent).HasDefaultValue(0);
        builder.Property(g => g.CreatedAt).IsRequired();
        builder.Property(g => g.IsDeleted).HasDefaultValue(false);

        builder.HasIndex(g => new { g.ClientId, g.CreatorProfileId, g.CreatedAt })
            .HasDatabaseName("IX_AiReferenceGenerations_Client_Creator_CreatedAt");
    }
}

public class AiReferenceImageConfiguration : IEntityTypeConfiguration<AiReferenceImage>
{
    public void Configure(EntityTypeBuilder<AiReferenceImage> builder)
    {
        builder.ToTable("AiReferenceImages");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.TempStorageKey).HasMaxLength(500).IsRequired();
        builder.Property(i => i.PermanentUrl).HasMaxLength(500);
        builder.Property(i => i.LabelVi).HasMaxLength(200).IsRequired();
        builder.Property(i => i.LabelEn).HasMaxLength(200).IsRequired();
        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.IsDeleted).HasDefaultValue(false);

        builder.HasOne(i => i.Generation)
            .WithMany(g => g.Images)
            .HasForeignKey(i => i.GenerationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => new { i.GenerationId, i.VariantIndex })
            .HasDatabaseName("IX_AiReferenceImages_Generation_VariantIndex");

        builder.HasIndex(i => i.CommissionId)
            .HasDatabaseName("IX_AiReferenceImages_CommissionId");
    }
}
