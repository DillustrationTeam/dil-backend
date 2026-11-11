using System.ComponentModel.DataAnnotations;

namespace ArtCommission.Application.Commission.DTOs;

public class CreateCommissionRequest
{
    public Guid CreatorId { get; set; }
    public Guid PackageId { get; set; }
    public List<Guid> AddonIds { get; set; } = [];
    public string LicenseType { get; set; } = "Personal";
    [Required, MinLength(1), MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset? DeadlineAt { get; set; }
    public string? VoucherCode { get; set; }
}

public class RespondCommissionRequest
{
    [Required, RegularExpression("^(Accept|Reject|Negotiate)$", ErrorMessage = "Action must be Accept, Reject, or Negotiate.")]
    public string Action { get; set; } = string.Empty; // Accept, Reject, Negotiate
    public decimal? NegotiatePrice { get; set; }
    public string? RejectReason { get; set; }
}

public class RespondToCounterofferRequest
{
    public bool Accept { get; set; }
}

public class SubmitMilestoneRequest
{
    [Required, MinLength(1)]
    public string WipFileUrl { get; set; } = string.Empty;
    public string? CreatorNote { get; set; }
}

/// <summary>DTO model for SCR-25 SubmitMilestoneWip endpoint.</summary>
public class SubmitMilestoneFormRequest
{
    public string? CreatorNote { get; set; }
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
    public List<string>? AttachedImageUrls { get; set; }
}

public class ReplyReviewRequest
{
    [Required, MinLength(1), MaxLength(1000)]
    public string ReplyComment { get; set; } = string.Empty;
}

public class CancelWithPolicyRequest
{
    [Required, MinLength(1), MaxLength(500)]
    public string CancellationReason { get; set; } = string.Empty;
    [MaxLength(2000)]
    public string? Details { get; set; }
}

public class SendTipRequest
{
    [Range(1000, 100_000_000, ErrorMessage = "Số tiền tip phải từ 1.000 VND đến 100.000.000 VND.")]
    public decimal Amount { get; set; }

    [MaxLength(500)]
    public string? Message { get; set; }
}

public record TipResultDto(
    Guid CommissionId,
    decimal Amount,
    decimal ClientRemainingBalance,
    string CreatorName,
    DateTimeOffset CreatedAt,
    string? Message
);

public class RenameCommissionRequest
{
    [Required(ErrorMessage = "Tên dự án không được để trống.")]
    [MinLength(1, ErrorMessage = "Tên dự án tối thiểu 1 ký tự.")]
    [MaxLength(200, ErrorMessage = "Tên dự án tối đa 200 ký tự.")]
    public string Title { get; set; } = string.Empty;
}

public record RenameCommissionResultDto(
    Guid CommissionId,
    string NewTitle,
    string UpdatedBy,
    Guid? RoomId,
    Guid? MessageId,
    string? MessageBody,
    DateTimeOffset? SentAt
);
