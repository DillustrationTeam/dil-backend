using ArtCommission.Domain.Entities.Auction;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Auction;

/// <summary>
/// EF Core mapping for the personalised-recommendation schema.
/// </summary>
public class UserInterestProfileConfiguration : IEntityTypeConfiguration<UserInterestProfile>
{
    public void Configure(EntityTypeBuilder<UserInterestProfile> builder)
    {
        builder.ToTable("UserInterestProfiles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProfileJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.SourceEventCount).HasDefaultValue(0);
        builder.Property(x => x.Version).HasDefaultValue(1);
        builder.Property(x => x.BuiltAt).IsRequired();
        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);

        builder.HasOne<Domain.Entities.Identity.ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UserId)
            .IsUnique()
            .HasDatabaseName("UX_UserInterestProfiles_UserId");
    }
}

public class RecommendationImpressionConfiguration : IEntityTypeConfiguration<RecommendationImpression>
{
    public void Configure(EntityTypeBuilder<RecommendationImpression> builder)
    {
        builder.ToTable("RecommendationImpressions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Surface).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(200);
        builder.Property(x => x.Score).HasPrecision(6, 4);
        builder.Property(x => x.ShownAt).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);

        builder.HasOne(x => x.Auction)
            .WithMany()
            .HasForeignKey(x => x.AuctionId)
            .OnDelete(DeleteBehavior.Cascade);

        // CTR per user/surface and per auction.
        builder.HasIndex(x => new { x.UserId, x.Surface, x.ShownAt })
            .HasDatabaseName("IX_RecommendationImpressions_User_Surface_ShownAt");

        builder.HasIndex(x => new { x.AuctionId, x.ShownAt })
            .HasDatabaseName("IX_RecommendationImpressions_Auction_ShownAt");
    }
}
