namespace ArtCommission.Application.Commission.DTOs;

public class CommissionDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid ClientId { get; set; }
    public string? ClientName { get; set; }
    public Guid CreatorId { get; set; }
    public Guid? PackageId { get; set; }
    public string? CreatorName { get; set; }
    public Guid? VoucherId { get; set; }
    public string LicenseType { get; set; } = "Personal";
    public decimal LicenseMultiplierApplied { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal FinalPrice { get; set; }
    public decimal EscrowHeldAmount { get; set; }
    public decimal DisbursedAmount { get; set; }
    public string EscrowStatus { get; set; } = string.Empty;
    public int CurrentStage { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? DeadlineAt { get; set; }
    public string? CancellationReason { get; set; }
    public string? CancellationDetails { get; set; }
    public Guid? CancellationRequestedById { get; set; }
    public string? CancellationRequestedByRole { get; set; }
    public string? CancellationRequestStatus { get; set; }
    public DateTimeOffset? CancellationRequestedAt { get; set; }
    public DateTimeOffset? CancellationRequestExpiresAt { get; set; }
    public bool IsOverdue { get; set; }
    public DateTimeOffset? OverdueGraceEndsAt { get; set; }
    public DateTimeOffset? ClientTermsAcceptedAt { get; set; }
    public string? ClientTermsVersion { get; set; }
    public DateTimeOffset? CreatorTermsAcceptedAt { get; set; }
    public string? CreatorTermsVersion { get; set; }
    public string CurrentWorkroomTermsVersion { get; set; } = "1.0";
    public DateTimeOffset CreatedAt { get; set; }
}

public class CommissionDetailDto : CommissionDto
{
    public List<MilestoneDto> Milestones { get; set; } = new();
    public ReviewDto? Review { get; set; }
    public ArtCommission.Application.Dispute.CommissionDispute.DTOs.CommissionDisputeDto? Dispute { get; set; }
}

public sealed record FinalDownloadDto(string DownloadUrl, DateTimeOffset ExpiresAt);

public sealed record RespondCancellationRequest(bool Approve);
