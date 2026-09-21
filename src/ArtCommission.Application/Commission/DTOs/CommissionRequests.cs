using System.ComponentModel.DataAnnotations;

namespace ArtCommission.Application.Commission.DTOs;

public class CreateCommissionRequest
{
    public Guid CreatorId { get; set; }
    [Required, MinLength(1), MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset? DeadlineAt { get; set; }
    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal TotalPrice { get; set; }
    public string? VoucherCode { get; set; }
    [Required, MinLength(1)]
    public List<MilestoneCreateDto> Milestones { get; set; } = new();
}

public class RespondCommissionRequest
{
    [Required, RegularExpression("^(Accept|Reject|Negotiate)$", ErrorMessage = "Action must be Accept, Reject, or Negotiate.")]
    public string Action { get; set; } = string.Empty; // Accept, Reject, Negotiate
    public decimal? NegotiatePrice { get; set; }
    public string? RejectReason { get; set; }
}

public class SubmitMilestoneRequest
{
    [Required, MinLength(1)]
    public string WipFileUrl { get; set; } = string.Empty;
}

public class RequestRevisionRequest
{
    [Required, MinLength(1)]
    public string FeedbackComment { get; set; } = string.Empty;
    public List<string>? ReferenceImages { get; set; }
}

public class CreateDisputeRequest
{
    [Required, MinLength(1)]
    public string Reason { get; set; } = string.Empty;
    public List<string>? EvidenceUrls { get; set; }
}

public class CreateReviewRequest
{
    [Range(1, 5)]
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

public class ReplyReviewRequest
{
    [Required, MinLength(1)]
    public string ReplyComment { get; set; } = string.Empty;
}
