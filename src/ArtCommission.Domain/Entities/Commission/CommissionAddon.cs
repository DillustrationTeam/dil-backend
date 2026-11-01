using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Commission;

public class CreatorAddon : BaseEntity
{
    public Guid CreatorId { get; set; }
    public Guid? PackageId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string AllowedMilestone { get; set; } = "Sketch";
    public int ExtendDays { get; set; }
    public string? PresetKey { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CommissionAddon : BaseEntity
{
    public Guid CommissionId { get; set; }
    public Guid ProposedById { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string AllowedMilestone { get; set; } = "Sketch";
    public int MilestoneSequence { get; set; }
    public string Status { get; set; } = "Proposed";
    public int ExtendDays { get; set; }
    public bool IsPreset { get; set; }
}
