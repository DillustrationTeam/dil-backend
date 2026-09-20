namespace ArtCommission.Application.ArtistStudio.Moderation.DTOs;

/// <summary>
/// DTO chứa thông tin chi tiết thanh tra tác phẩm kèm kết quả scan AI và danh sách tag (SCR-20).
/// </summary>
public record ArtworkInspectionDetailDto
{
    public Guid ArtworkId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string ImageUrl { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public string? WatermarkedUrl { get; init; }
    public string? Style { get; init; }
    public int LikeCount { get; init; }
    public string? Resolution { get; init; }
    public long? FileSizeBytes { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public int ViewCount { get; init; }

    // AI Confidence Scores
    public decimal? SafeScore { get; init; }
    public decimal? AdultScore { get; init; }
    public decimal? ViolenceScore { get; init; }
    public bool IsAiGenerated { get; init; }
    public decimal? AiDetectionScore { get; init; }

    // Moderation Info
    public string? FlagReason { get; init; }
    public string ModerationStatus { get; init; } = "Pending";
    public Guid? ModeratorId { get; init; }
    public string? ModeratorName { get; init; }
    public string? ModerationNote { get; init; }
    public DateTimeOffset? ModeratedAt { get; init; }

    // Creator Info
    public Guid CreatorProfileId { get; init; }
    public string CreatorName { get; init; } = string.Empty;
    public string CreatorUsername { get; init; } = string.Empty;
    public string? CreatorAvatarUrl { get; init; }

    // Tags
    public List<ArtworkTagItemDto> Tags { get; init; } = new();
}

public record ArtworkTagItemDto(Guid TagId, string Name, bool IsAiGenerated);
