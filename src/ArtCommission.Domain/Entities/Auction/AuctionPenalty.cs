using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Auction;

/// <summary>
/// One confirmed auction delivery dispute against a seller.
///
/// The escalation ladder has five steps and is shared by both payout situations:
///   - the escrow was still held, so the buyer got the money back  -> first case enters at step 1
///   - the escrow was already released to the seller (buyer had accepted the digital
///     copy first)                                               -> first case enters at step 3
///
/// Step 3 and 4 additionally carry a surcharge on the platform fee of the next sold
/// sessions; step 2 and 4 temporarily block creating new auction sessions.
/// </summary>
public class AuctionPenalty : BaseEntity
{
    public Guid SellerId { get; set; }

    /// <summary>Escalation step this case landed on, 1..5.</summary>
    public int StrikeLevel { get; set; }

    /// <summary>
    /// Extra percentage points added on top of the platform fee while the penalty is
    /// active: 20 for step 3, 40 for step 4, null when the step carries no surcharge.
    /// </summary>
    public decimal? SurchargePercent { get; set; }

    /// <summary>Number of sold sessions the surcharge covers (0 when the step has none).</summary>
    public int SessionsTotal { get; set; }

    /// <summary>Sold sessions the surcharge has already been applied to.</summary>
    public int SessionsUsed { get; set; }

    /// <summary>End of the temporary selling suspension, null when the step has none.</summary>
    public DateTimeOffset? BanUntil { get; set; }

    /// <summary>True when the ladder skipped the two warning steps because the money was already paid out.</summary>
    public bool EscrowReleasedAtStrike { get; set; }

    public PenaltyStatus Status { get; set; } = PenaltyStatus.Active;

    // Traceability
    public Guid SourceAuctionId { get; set; }
    public Guid? SourceDeliveryId { get; set; }
    public Guid? SourceDisputeResolvedByAdminId { get; set; }

    /// <summary>Short, human readable reason shown in the seller dashboard.</summary>
    public string? Reason { get; set; }

    public Guid? RevokedByAdminId { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokeReason { get; set; }

    public Auction? Auction { get; set; }
}
