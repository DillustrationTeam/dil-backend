namespace ArtCommission.Application.Commission.DTOs;

public class CreateCommissionRequest
{
    public Guid CreatorId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset? DeadlineAt { get; set; }
    public decimal TotalPrice { get; set; }
    public string? VoucherCode { get; set; }
    public List<MilestoneCreateDto> Milestones { get; set; } = new();
}

public class RespondCommissionRequest
{
    public string Action { get; set; } = string.Empty; // Accept, Reject, Negotiate
    public decimal? NegotiatePrice { get; set; }
    public string? RejectReason { get; set; }
}

public class SubmitMilestoneRequest
{
    public string WipFileUrl { get; set; } = string.Empty;
}

public class RequestRevisionRequest
{
    public string FeedbackComment { get; set; } = string.Empty;
    public List<string>? ReferenceImages { get; set; }
}

public class CreateDisputeRequest
{
    public string Reason { get; set; } = string.Empty;
    public List<string>? EvidenceUrls { get; set; }
}

public class CreateReviewRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

public class ReplyReviewRequest
{
    public string ReplyComment { get; set; } = string.Empty;
}
