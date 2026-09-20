namespace ArtCommission.Application.Admin.Moderation.DTOs;

/// <summary>
/// DTO biểu diễn một tác phẩm trong hàng đợi kiểm duyệt (SCR-20).
/// </summary>
public record ModerationQueueItemDto
{
    public Guid ArtworkId { get; init; }
    public string Title { get; init; } = string.Empty;
    public Guid CreatorProfileId { get; init; }
    public string CreatorName { get; init; } = string.Empty;
    public string CreatorUsername { get; init; } = string.Empty;
    public string? CreatorAvatarUrl { get; init; }
    public string ImageUrl { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }

    /// <summary>
    /// Tỷ lệ an toàn được phát hiện bởi AI (e.g. 0.984 tương đương 98.4% SAFE).
    /// </summary>
    public decimal? SafeScore { get; init; }

    /// <summary>
    /// Lý do đưa vào hàng đợi kiểm duyệt (e.g. "AI NSFW Threshold Borderline", "User Reported").
    /// </summary>
    public string? FlagReason { get; init; }

    /// <summary>
    /// Trạng thái kiểm duyệt: "Pending", "Approved", "Rejected", "Hidden".
    /// </summary>
    public string ModerationStatus { get; init; } = "Pending";

    public bool IsAiGenerated { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
