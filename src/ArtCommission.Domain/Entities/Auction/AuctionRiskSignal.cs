using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Auction;

/// <summary>
/// Append-only evidence row: one detected pattern on one auction.
///
/// Never updated after insert - a re-scan writes new rows with a newer
/// <see cref="DetectorVersion"/> and a new <see cref="AuctionRiskAssessment"/>.
/// Keeping every detection makes a later moderation decision auditable ("why was this flagged
/// on 12/03") without reloading historical bid data.
///
/// Personal data: <see cref="EvidenceJson"/> must stay masked (partial IP, bidder alias,
/// counters). It is served only to Administrator/Moderator roles.
/// </summary>
public class AuctionRiskSignal : BaseEntity
{
    public Guid AuctionId { get; set; }

    /// <summary>Bid that triggered the rule, when the rule is bid-scoped (step jumps, velocity).</summary>
    public Guid? BidId { get; set; }

    /// <summary>Actor the signal is about (bidder or seller). Null for auction-wide patterns.</summary>
    public Guid? UserId { get; set; }

    public RiskSignalCode SignalCode { get; set; }

    /// <summary>Points contributed to the assessment score. Tunable per code via PlatformConfig.</summary>
    public decimal Weight { get; set; }

    /// <summary>Masked, human-readable evidence consumed by the moderation evidence sheet.</summary>
    public string EvidenceJson { get; set; } = string.Empty;

    /// <summary>Detector build that produced the row, e.g. <c>rules-v1</c>.</summary>
    public string DetectorVersion { get; set; } = "rules-v1";

    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public Auction? Auction { get; set; }
    public Bid? Bid { get; set; }
}
