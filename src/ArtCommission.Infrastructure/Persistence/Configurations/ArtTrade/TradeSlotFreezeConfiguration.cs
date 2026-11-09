using ArtCommission.Domain.Entities.Trade;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.ArtTrade;

public class TradeSlotFreezeConfiguration : IEntityTypeConfiguration<TradeSlotFreeze>
{
    public void Configure(EntityTypeBuilder<TradeSlotFreeze> builder)
    {
        builder.ToTable("TradeSlotFreezes");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Reason)
            .HasMaxLength(500);

        builder.Property(f => f.RevokedReason)
            .HasMaxLength(500);

        // Foreign keys & Delete behaviors
        builder.HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.ArtTrade)
            .WithMany()
            .HasForeignKey(f => f.ArtTradeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.RevokedByAdmin)
            .WithMany()
            .HasForeignKey(f => f.RevokedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        // Performance indexes for checking active freezes:
        // WHERE UserId = @userId AND FrozenUntil > @now AND IsRevoked = 0 AND IsDeleted = 0
        builder.HasIndex(f => new { f.UserId, f.FrozenUntil, f.IsRevoked, f.IsDeleted })
            .HasDatabaseName("IX_TradeSlotFreezes_User_Active");

        builder.HasIndex(f => f.ArtTradeId)
            .HasDatabaseName("IX_TradeSlotFreezes_ArtTradeId");
    }
}

