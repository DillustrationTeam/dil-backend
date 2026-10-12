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
    public string Status { get; set; } = "Pending"; // Pending, UnderReview, Resolved, Rejected
    public string? Resolution { get; set; }          // ClientWin100, ArtistWin100, SplitCustom
    public decimal? ClientRefundAmount { get; set; }
    public decimal? ArtistPayAmount { get; set; }
    public string? AdminNote { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class CreateCommissionDisputeRequest
{
    public string Reason { get; set; } = string.Empty;
    public List<string>? EvidenceUrls { get; set; }
}

public class ResolveCommissionDisputeRequest
{
    public string Resolution { get; set; } = string.Empty; // ClientWin100, ArtistWin100, SplitCustom
    public decimal? ClientRefundAmount { get; set; }
    public decimal? ArtistPayAmount { get; set; }
    public string? AdminNote { get; set; }
}
