using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Auction;

/// <summary>
/// Aggregated verdict for one auction: score, bucket, review state and the moderator's decision.
///
/// Invariant: only one row per auction may be <see cref="AuctionRiskAssessmentStatus.Open"/> or
/// <see cref="AuctionRiskAssessmentStatus.UnderReview"/> (filtered unique index); resolved rows
/// accumulate as history.
///
/// The detector NEVER cancels an auction or punishes a user by itself - it writes this row, the
/// notification fan-out tells administrators, and a human decides.
/// </summary>
public class AuctionRiskAssessment : BaseEntity
{
    public Guid AuctionId { get; set; }

    /// <summary>Sum of the contributing signal weights, clamped to 0-100 by the scorer.</summary>
    public decimal Score { get; set; }

    public AuctionRiskLevel RiskLevel { get; set; } = AuctionRiskLevel.Low;

    public AuctionRiskAssessmentStatus Status { get; set; } = AuctionRiskAssessmentStatus.Open;

    /// <summary>How many evidence rows backed this assessment (cheap counter for the queue list).</summary>
    public int SignalCount { get; set; }

    /// <summary>One-paragraph Vietnamese explanation for the moderator. Template-based, optional LLM polish.</summary>
    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Moderator currently holding the sheet (status UnderReview). Lets two operators avoid
    /// reviewing the same auction twice; the claim expires so an abandoned sheet returns to the queue.
    /// </summary>
    public Guid? ClaimedByUserId { get; set; }

    public DateTimeOffset? ClaimedAt { get; set; }

    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>Mandatory when the moderator confirms fraud; kept for later dispute appeals.</summary>
    public string? DecisionNote { get; set; }

    /// <summary>Detector build that produced the score, e.g. <c>rules-v1</c>.</summary>
    public string ModelVersion { get; set; } = "rules-v1";

    /// <summary>Concurrency token so two moderators cannot resolve the same sheet at once.</summary>
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public Auction? Auction { get; set; }
}
