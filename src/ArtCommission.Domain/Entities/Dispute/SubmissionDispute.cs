using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Dispute;

public class SubmissionDispute : BaseEntity
{
    public Guid EventId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid RaisedById { get; set; }
    public string Priority { get; set; } = "Medium";     // Low, Medium, High
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceUrls { get; set; }
    public string Status { get; set; } = "Pending";      // Pending, UnderReview, Resolved, Rejected
    public string? Resolution { get; set; }              // Dismiss, Disqualify, RequestReEvaluation, Other
    public string? Note { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    // Navigation properties
    public Event.PlatformEvent Event { get; set; } = null!;
    public Event.EventSubmission Submission { get; set; } = null!;
}
