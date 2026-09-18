namespace ArtCommission.Application.Commission.DTOs;

public class CommissionDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid ClientId { get; set; }
    public string? ClientName { get; set; }
    public Guid CreatorId { get; set; }
    public string? CreatorName { get; set; }
    public Guid? VoucherId { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal FinalPrice { get; set; }
    public decimal EscrowHeldAmount { get; set; }
    public decimal DisbursedAmount { get; set; }
    public string EscrowStatus { get; set; } = string.Empty;
    public int CurrentStage { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? DeadlineAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class CommissionDetailDto : CommissionDto
{
    public List<MilestoneDto> Milestones { get; set; } = new();
    public ReviewDto? Review { get; set; }
    public DisputeDto? Dispute { get; set; }
}
