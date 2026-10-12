namespace ArtCommission.Application.Dispute.SubmissionDispute.DTOs;

public class SubmissionDisputeDto
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string? EventTitle { get; set; }
    public Guid SubmissionId { get; set; }
    public string? SubmissionTitle { get; set; }
    public string? ArtworkImageUrl { get; set; }
    public Guid RaisedById { get; set; }
    public string? RaisedByName { get; set; }
    public string Category { get; set; } = string.Empty; // Copyright, ScoreAppeal, AiArt, VoteFraud
    public string Priority { get; set; } = "Medium";     // Low, Medium, High
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceUrls { get; set; }
    public List<string>? EvidenceUrlList { get; set; }
    public string Status { get; set; } = "Pending";      // Pending, UnderReview, Resolved, Rejected
    public string? Resolution { get; set; }              // Dismiss, Disqualify, AdjustScore, DeductVotes
    public string? Note { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class CreateSubmissionDisputeRequest
{
    public Guid SubmissionId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string>? EvidenceUrls { get; set; }
    public string Priority { get; set; } = "Medium";
}

public class ResolveSubmissionDisputeRequest
{
    public string Resolution { get; set; } = string.Empty; // Dismiss, Disqualify, AdjustScore, DeductVotes
    public string? Note { get; set; }
}
