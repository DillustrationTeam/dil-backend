using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Auction;

/// <summary>
/// EF Core mapping for the auction fraud-detection schema (AI Bid Fraud Detection).
///
/// Two tables, both written by the detector only:
///   - <c>AuctionRiskSignals</c>  - append-only evidence, one row per detected pattern.
///   - <c>AuctionRiskAssessments</c> - aggregated verdict with the moderator decision.
///
/// Column types follow the module convention: enums stored as strings (readable in SQL),
/// money-like decimals pinned to an explicit precision, soft-delete flag defaulted to 0.
/// </summary>
public class AuctionRiskSignalConfiguration : IEntityTypeConfiguration<AuctionRiskSignal>
{
    public void Configure(EntityTypeBuilder<AuctionRiskSignal> builder)
    {
        builder.ToTable("AuctionRiskSignals");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SignalCode)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.Weight)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.EvidenceJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.DetectorVersion)
            .HasMaxLength(32)
            .HasDefaultValue("rules-v1")
            .IsRequired();

        builder.Property(x => x.DetectedAt).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);

        // Evidence is worthless without its auction, so it follows the auction row.
        builder.HasOne(x => x.Auction)
            .WithMany()
            .HasForeignKey(x => x.AuctionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not SetNull: SQL Server rejects multiple cascade paths here (Bids already cascade
        // from Auctions, so a second cascading path into AuctionRiskSignals is illegal). Evidence is
        // an audit trail anyway - a bid referenced by a signal must not be hard-deleted.
        builder.HasOne(x => x.Bid)
            .WithMany()
            .HasForeignKey(x => x.BidId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Entities.Identity.ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.AuctionId, x.DetectedAt })
            .HasDatabaseName("IX_AuctionRiskSignals_Auction_DetectedAt");

        builder.HasIndex(x => new { x.SignalCode, x.DetectedAt })
            .HasDatabaseName("IX_AuctionRiskSignals_Code_DetectedAt");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("IX_AuctionRiskSignals_UserId");
    }
}

public class AuctionRiskAssessmentConfiguration : IEntityTypeConfiguration<AuctionRiskAssessment>
{
    public void Configure(EntityTypeBuilder<AuctionRiskAssessment> builder)
    {
        builder.ToTable("AuctionRiskAssessments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Score)
            .HasPrecision(5, 2)
            .IsRequired();

        // No HasDefaultValue here: both enums start at 1, so CLR default 0 is not a valid member
        // and a database-generated default would silently override explicit writes.
        builder.Property(x => x.RiskLevel)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.SignalCount).HasDefaultValue(0);
        builder.Property(x => x.Summary).HasMaxLength(1000);
        builder.Property(x => x.DecisionNote).HasMaxLength(1000);
        builder.Property(x => x.ModelVersion).HasMaxLength(32).HasDefaultValue("rules-v1").IsRequired();

        builder.Property(x => x.DetectedAt).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);

        // Two moderators must not resolve the same sheet concurrently.
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.Auction)
            .WithMany()
            .HasForeignKey(x => x.AuctionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Business invariant: one live sheet per auction, history may repeat.
        builder.HasIndex(x => x.AuctionId)
            .IsUnique()
            .HasFilter("[Status] IN (N'Open', N'UnderReview')")
            .HasDatabaseName("UX_AuctionRiskAssessments_Auction_Live");

        // Queue reads: open sheets ordered by severity then score.
        builder.HasIndex(x => new { x.Status, x.RiskLevel, x.Score })
            .HasDatabaseName("IX_AuctionRiskAssessments_Queue");
    }
}
