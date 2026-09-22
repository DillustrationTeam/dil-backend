namespace ArtCommission.Application.Commission.DTOs;

public class MilestoneDto
{
    public Guid Id { get; set; }
    public Guid CommissionId { get; set; }
    public int Sequence { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? WipPreviewUrl { get; set; }
    public string? WatermarkedUrl { get; set; }
    public string? FinalDeliverableUrl { get; set; }
    public int RevisionCount { get; set; }
    public int MaxRevisions { get; set; }
    public bool RevisionLimitReached => RevisionCount >= MaxRevisions;
    public string? CreatorNote { get; set; }
    public string? RevisionFeedback { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}

public class MilestoneCreateDto
{
    public int Sequence { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
