using System.ComponentModel.DataAnnotations;

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
    [Range(1, int.MaxValue)]
    public int Sequence { get; set; }
    [Required, MinLength(1), MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal Price { get; set; }
}
