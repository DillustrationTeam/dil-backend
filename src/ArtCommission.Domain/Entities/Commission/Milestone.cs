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

    /// <summary>URL ảnh WIP gốc (không watermark) — lưu trong private bucket.</summary>
    public string? OriginalWipUrl { get; set; }

    /// <summary>URL ảnh WIP đã áp Watermark — lưu trong public CDN.</summary>
    public string? WipPreviewUrl { get; set; }
    public string? WatermarkedUrl { get; set; }

    /// <summary>Storage key của file master (PSD/ZIP) trong private bucket.</summary>
    public string? FinalDeliverableUrl { get; set; }

    public int RevisionCount { get; set; } = 0;

    /// <summary>Giới hạn số lần revision miễn phí mỗi milestone.</summary>
    public int MaxRevisions { get; set; } = 3;

    /// <summary>Ghi chú của Creator khi nộp milestone.</summary>
    public string? CreatorNote { get; set; }

    /// <summary>Feedback thô của Client khi yêu cầu sửa.</summary>
    public string? RevisionFeedback { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }

    // Navigation property
    public Commission Commission { get; set; } = null!;
}
