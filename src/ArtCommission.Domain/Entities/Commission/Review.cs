using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Commission;

public class Review : BaseEntity
{
    public Guid CommissionId { get; set; }
    public Guid ReviewerId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? ReviewerReply { get; set; }
    public bool IsVisible { get; set; } = true;

    /// <summary>JSON array của tối đa 3 URLs ảnh đính kèm review (public bucket).</summary>
    public string? AttachedImagesJson { get; set; }

    /// <summary>Timestamp khi Creator phản hồi đánh giá.</summary>
    public DateTimeOffset? RespondedAt { get; set; }

    // Navigation property
    public Commission Commission { get; set; } = null!;
}

