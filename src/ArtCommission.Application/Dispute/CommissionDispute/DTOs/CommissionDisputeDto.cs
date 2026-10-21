namespace ArtCommission.Application.Dispute.CommissionDispute.DTOs;

public class CommissionDisputeDto
{
    public Guid Id { get; set; }
    public Guid CommissionId { get; set; }
    public string? CommissionTitle { get; set; }
    public Guid RaisedById { get; set; }
    public string? RaisedByName { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceUrls { get; set; }
    public string Status { get; set; } = "Negotiating"; // Negotiating, Pending, UnderReview, Resolved, Rejected
    public string? Resolution { get; set; }          // ClientWin100, ArtistWin100, SplitCustom, MutualSettled
    public decimal? ClientRefundAmount { get; set; }
    public decimal? ArtistPayAmount { get; set; }
    public decimal? ClientRefundPercent { get; set; }
    public string? AdminNote { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // 2-Party Negotiation (Two-Party Dispute Resolution Flow)
    public decimal? ClaimantRefundPercent { get; set; }
    public decimal? RespondentRefundPercent { get; set; }
    public string? RespondentResponse { get; set; }
    public string? RespondentEvidenceUrls { get; set; }
    public DateTimeOffset? RespondentRespondedAt { get; set; }
}

public class CreateCommissionDisputeRequest
{
    public string Reason { get; set; } = string.Empty;
    public List<string>? EvidenceUrls { get; set; }
    public decimal? ClaimantRefundPercent { get; set; } = 100;
}

public class ResolveCommissionDisputeRequest
{
    public string Resolution { get; set; } = string.Empty; // ClientWin100, ArtistWin100, SplitCustom
    public decimal? ClientRefundAmount { get; set; }
    public decimal? ArtistPayAmount { get; set; }
    public decimal? ClientRefundPercent { get; set; }
    public string? AdminNote { get; set; }
}
