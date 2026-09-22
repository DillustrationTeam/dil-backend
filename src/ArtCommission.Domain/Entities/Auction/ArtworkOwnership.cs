using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Auction;

/// <summary>
/// Sổ chủ sở hữu tranh — append-only theo thời gian.
///
/// Bất biến quan trọng nhất: mỗi <see cref="ArtworkId"/> chỉ có TỐI ĐA MỘT dòng
/// <see cref="IsCurrent"/> = true. Được ép bằng unique filtered index
/// <c>UX_ArtworkOwnership_Current</c> (WHERE IsCurrent = 1), vì ràng buộc này
/// không thể biểu diễn bằng check constraint thông thường.
///
/// Hệ quả khi chuyển quyền: phải set dòng cũ <c>IsCurrent = false</c> TRƯỚC khi
/// thêm dòng mới, trong cùng một transaction — nếu không index sẽ chặn.
/// </summary>
public class ArtworkOwnership : BaseEntity
{
    public Guid ArtworkId { get; set; }

    /// <summary>Chủ sở hữu hiện tại (hoặc trong quá khứ) của tranh.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Thời điểm bắt đầu sở hữu.</summary>
    public DateTimeOffset AcquiredAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Thời điểm kết thúc sở hữu. Null nghĩa là đang sở hữu.</summary>
    public DateTimeOffset? ReleasedAt { get; set; }

    public OwnershipTransferReason TransferReason { get; set; } = OwnershipTransferReason.AuctionWin;

    /// <summary>Phiên đấu giá dẫn tới lần chuyển quyền này (nếu có).</summary>
    public Guid? AuctionId { get; set; }

    /// <summary>Đơn đặt vẽ dẫn tới lần chuyển quyền này (nếu có).</summary>
    public Guid? CommissionId { get; set; }

    /// <summary>True nếu là chủ sở hữu hiện tại. Tối đa một dòng true cho mỗi tranh.</summary>
    public bool IsCurrent { get; set; }
}
