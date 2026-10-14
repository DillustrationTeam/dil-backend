namespace ArtCommission.Application.Commission.Disputes.DTOs;

/// <summary>
/// DTO chứa chi tiết hồ sơ trọng tài tranh chấp: claim, commission, escrow amount, milestones và chat snapshot (SCR-22 / UC30).
/// </summary>
public record DisputeArbitrationDetailDto
{
    public Guid DisputeId { get; init; }
    public Guid CommissionId { get; init; }
    public string CommissionTitle { get; init; } = string.Empty;
    public string? CommissionDescription { get; init; }
    public string CommissionStatus { get; init; } = string.Empty;
    public string EscrowStatus { get; init; } = string.Empty;

    public decimal TotalPrice { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal FinalPrice { get; init; }
    public decimal EscrowHeldAmount { get; init; }
    public decimal DisbursedAmount { get; init; }
    public int CurrentStage { get; init; }
    public DateTimeOffset? DeadlineAt { get; init; }

    public DisputePartyDto Client { get; init; } = null!;
    public DisputePartyDto Creator { get; init; } = null!;
    public DisputeRaiserDto RaisedBy { get; init; } = null!;

    public string Reason { get; init; } = string.Empty;
    public List<string> EvidenceUrls { get; init; } = new();

    public string Status { get; init; } = "Pending"; // Pending, UnderReview, Resolved
    public string? Resolution { get; init; } // ClientWin100, ArtistWin100, SplitCustom
    public decimal? ClientRefundAmount { get; init; }
    public decimal? ArtistPayAmount { get; init; }
    public string? AdminNote { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }

    public List<DisputeMilestoneDto> Milestones { get; init; } = new();
    public List<DisputeChatSnapshotMessageDto> ChatSnapshot { get; init; } = new();
}
