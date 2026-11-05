using ArtCommission.Domain.Entities.Dispute;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.ArtTrade;

public class TradeDisputeConfiguration : IEntityTypeConfiguration<TradeDispute>
{
    public void Configure(EntityTypeBuilder<TradeDispute> builder)
    {
        builder.ToTable("TradeDisputes");

        builder.HasKey(d => d.Id);

        builder.HasIndex(d => d.ArtTradeId);

        builder.Property(d => d.Reason)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(d => d.Status)
            .HasMaxLength(50);

        builder.Property(d => d.Resolution)
            .HasMaxLength(50);

        builder.Property(d => d.AdminNote)
            .HasMaxLength(2000);

        builder.HasOne(d => d.ArtTrade)
            .WithMany(t => t.Disputes)
            .HasForeignKey(d => d.ArtTradeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

