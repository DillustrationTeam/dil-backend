using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Dispute;

public class CommissionDispute : BaseEntity
{
    public Guid CommissionId { get; set; }
    public Guid RaisedById { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceUrls { get; set; }
    public string Status { get; set; } = "Negotiating"; // Negotiating, Pending, UnderReview, Resolved, Rejected
    public string? Resolution { get; set; }          // ClientWin100, ArtistWin100, SplitCustom, MutualSettled
    public decimal? ClientRefundAmount { get; set; }
    public decimal? ArtistPayAmount { get; set; }
    public string? AdminNote { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    // 2-Party Negotiation (Two-Party Dispute Resolution Flow)
    public decimal? ClaimantRefundPercent { get; set; }
    public decimal? RespondentRefundPercent { get; set; }
    public string? RespondentResponse { get; set; }
    public string? RespondentEvidenceUrls { get; set; }
    public DateTimeOffset? RespondentRespondedAt { get; set; }

    // Navigation property
    public Commission.Commission Commission { get; set; } = null!;
}
