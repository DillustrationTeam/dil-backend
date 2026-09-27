using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Auction;

/// <summary>
/// Người dùng theo dõi một phiên đấu giá để nhận cảnh báo sắp kết thúc / bị đè giá.
/// Quan hệ nhiều-nhiều giữa <see cref="Identity.ApplicationUser"/> và <see cref="Auction"/>.
///
/// Bất biến: mỗi cặp (AuctionId, UserId) tối đa một dòng — bảo vệ bằng unique index,
/// nên handler watch phải idempotent (bấm 2 lần không sinh 2 dòng).
/// </summary>
public class AuctionWatch : BaseEntity
{
    public Guid AuctionId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Bật/tắt nhận cảnh báo real-time cho phiên này.</summary>
    public bool NotifyOnOutbid { get; set; } = true;

    /// <summary>Bật/tắt cảnh báo sắp hết giờ.</summary>
    public bool NotifyEndingSoon { get; set; } = true;

    // Navigation
    public Auction? Auction { get; set; }
}
