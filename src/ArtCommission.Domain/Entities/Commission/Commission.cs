using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Commission;

public class Commission : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid ClientId { get; set; }
    public Guid CreatorId { get; set; }
    public Guid? VoucherId { get; set; }

    public decimal DiscountAmount { get; set; } = 0.00m;
    public decimal TotalPrice { get; set; } = 0.00m;
    public decimal FinalPrice { get; set; } = 0.00m;
    public decimal EscrowHeldAmount { get; set; } = 0.00m;
    public decimal DisbursedAmount { get; set; } = 0.00m;

    public EscrowStatus EscrowStatus { get; set; } = EscrowStatus.Pending;
    public int CurrentStage { get; set; } = 1;
    public CommissionStatus Status { get; set; } = CommissionStatus.PendingAcceptance;
    public DateTimeOffset? DeadlineAt { get; set; }

    // Navigation properties
    public ICollection<Milestone> Milestones { get; set; } = new List<Milestone>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Dispute> Disputes { get; set; } = new List<Dispute>();
}
