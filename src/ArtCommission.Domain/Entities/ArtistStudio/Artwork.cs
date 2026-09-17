using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.ArtistStudio;

public class Artwork : BaseEntity
{
    public Guid CreatorProfileId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public bool IsAiGenerated { get; set; }
    public decimal? AiDetectionScore { get; set; }
    public string ModerationStatus { get; set; } = "Pending";
    public int ViewCount { get; set; }

    public CreatorProfile? CreatorProfile { get; set; }
    public ICollection<ArtworkTag> ArtworkTags { get; set; } = new List<ArtworkTag>();
}
