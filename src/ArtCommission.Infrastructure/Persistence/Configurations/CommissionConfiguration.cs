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
            .HasMaxLength(50);

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

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
