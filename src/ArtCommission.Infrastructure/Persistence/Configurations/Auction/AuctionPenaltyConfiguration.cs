using ArtCommission.Domain.Entities.Auction;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Auction;

public class AuctionPenaltyConfiguration : IEntityTypeConfiguration<AuctionPenalty>
{
    public void Configure(EntityTypeBuilder<AuctionPenalty> builder)
    {
        builder.ToTable("AuctionPenalties");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.StrikeLevel).IsRequired();
        builder.Property(p => p.SurchargePercent).HasPrecision(5, 2);
        builder.Property(p => p.Reason).HasMaxLength(500);
        builder.Property(p => p.RevokeReason).HasMaxLength(500);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        // One penalty per resolved dispute: resolving the same dispute twice must never
        // escalate the seller ladder twice.
        builder.HasIndex(p => p.SourceDeliveryId)
            .IsUnique()
            .HasFilter("[SourceDeliveryId] IS NOT NULL AND [IsDeleted] = 0");

        // Settlement and the dashboard both look up the penalties of one seller.
        builder.HasIndex(p => new { p.SellerId, p.Status });

        builder.HasOne(p => p.Auction)
            .WithMany()
            .HasForeignKey(p => p.SourceAuctionId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
