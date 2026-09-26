using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.ArtistStudio;

public class Artwork : BaseEntity
{
    public Guid CreatorProfileId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? WatermarkedUrl { get; set; } // Ảnh có watermark bảo hộ tác quyền (database.sql)
    public string? Style { get; set; } // Phong cách tranh (database.sql)
    public int LikeCount { get; set; } // Lượt thích (database.sql)
    public bool IsAiGenerated { get; set; }
    public decimal? AiDetectionScore { get; set; }
    public string ModerationStatus { get; set; } = "Pending";
    public int ViewCount { get; set; }
    public string? Style { get; set; }
    public string LicenseType { get; set; } = "Personal";
    public decimal? StartingPrice { get; set; }
    public bool AutoWatermarkEnabled { get; set; } = true;
    public bool AutoTaggingEnabled { get; set; } = true;

    // AI Vision Scan Confidence Metrics (SCR-20)
    public decimal? SafeScore { get; set; }
    public decimal? AdultScore { get; set; }
    public decimal? ViolenceScore { get; set; }

    // Artwork Metadata & Moderation Reason
    public string? FlagReason { get; set; }
    public string? Resolution { get; set; }
    public long? FileSizeBytes { get; set; }

    // Moderation Audit Trail
    public Guid? ModeratorId { get; set; }
    public string? ModerationNote { get; set; }
    public DateTimeOffset? ModeratedAt { get; set; }

    public CreatorProfile? CreatorProfile { get; set; }
    public ICollection<ArtworkTag> ArtworkTags { get; set; } = new List<ArtworkTag>();
}
