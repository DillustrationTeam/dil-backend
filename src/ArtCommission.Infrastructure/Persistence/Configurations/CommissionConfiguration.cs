using ArtCommission.Domain.Entities.Commission;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations;

public class CommissionConfiguration : IEntityTypeConfiguration<Commission>
{
    public void Configure(EntityTypeBuilder<Commission> builder)
    {
        builder.ToTable("Commissions");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.UpdatedAt).IsConcurrencyToken();

        builder.Property(c => c.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.DiscountAmount)
            .HasPrecision(18, 2);

        builder.Property(c => c.LicenseType)
            .HasMaxLength(20);

        builder.Property(c => c.LicenseMultiplierApplied)
            .HasPrecision(5, 2);

        builder.Property(c => c.TotalPrice)
            .HasPrecision(18, 2);

        builder.Property(c => c.FinalPrice)
            .HasPrecision(18, 2);

        builder.Property(c => c.EscrowHeldAmount)
            .HasPrecision(18, 2);

        builder.Property(c => c.DisbursedAmount)
            .HasPrecision(18, 2);

        builder.Property(c => c.EscrowStatus)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsConcurrencyToken();

        builder.Property(c => c.CurrentStage).IsConcurrencyToken();

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsConcurrencyToken();

        builder.HasIndex(c => new { c.ClientId, c.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Commissions_ClientId_CreatedAt");

        builder.HasIndex(c => new { c.CreatorId, c.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Commissions_CreatorId_CreatedAt");

        builder.HasIndex(c => new { c.Status, c.UpdatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Commissions_Status_UpdatedAt");

        builder.HasMany(c => c.Milestones)
            .WithOne(m => m.Commission)
            .HasForeignKey(m => m.CommissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Reviews)
            .WithOne(r => r.Commission)
            .HasForeignKey(r => r.CommissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Disputes)
            .WithOne(d => d.Commission)
            .HasForeignKey(d => d.CommissionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
