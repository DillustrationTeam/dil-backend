using ArtCommission.Domain.Entities.Dispute;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Dispute;

public class CommissionDisputeConfiguration : IEntityTypeConfiguration<CommissionDispute>
{
    public void Configure(EntityTypeBuilder<CommissionDispute> builder)
    {
        builder.ToTable("Disputes");

        builder.HasKey(d => d.Id);

        builder.HasIndex(d => d.CommissionId).IsUnique();

        builder.Property(d => d.Reason)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(d => d.Status)
            .HasMaxLength(50);

        builder.Property(d => d.Resolution)
            .HasMaxLength(50);

        builder.Property(d => d.ClientRefundAmount)
            .HasPrecision(18, 2);

        builder.Property(d => d.ArtistPayAmount)
            .HasPrecision(18, 2);
    }
}
