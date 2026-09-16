using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Commission;

public class Milestone : BaseEntity
{
    public Guid CommissionId { get; set; }
    public int Sequence { get; set; } = 1;
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; } = 0.00m;
    public MilestoneStatus Status { get; set; } = MilestoneStatus.Pending;

    public string? WipPreviewUrl { get; set; }
    public string? WatermarkedUrl { get; set; }
    public string? FinalDeliverableUrl { get; set; }
    public int RevisionCount { get; set; } = 0;

    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }

    // Navigation property
    public Commission Commission { get; set; } = null!;
}
