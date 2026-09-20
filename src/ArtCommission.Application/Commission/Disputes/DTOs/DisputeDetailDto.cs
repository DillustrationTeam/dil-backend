namespace ArtCommission.Application.Commission.Disputes.DTOs;

/// <summary>
/// DTO chứa chi tiết vụ tranh chấp phục vụ Moderator/Admin thẩm định và ra phán quyết (SCR-22 / UC30).
/// </summary>
public record DisputeDetailDto
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
}

public record DisputePartyDto
{
    public Guid UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public record DisputeRaiserDto
{
    public Guid UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty; // "Client" hoặc "Creator"
}

public record DisputeMilestoneDto
{
    public Guid Id { get; init; }
    public int Sequence { get; init; }
    public string Title { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? WipPreviewUrl { get; init; }
    public string? WatermarkedUrl { get; init; }
    public string? FinalDeliverableUrl { get; init; }
    public int RevisionCount { get; init; }
    public DateTimeOffset? SubmittedAt { get; init; }
    public DateTimeOffset? ApprovedAt { get; init; }
}
