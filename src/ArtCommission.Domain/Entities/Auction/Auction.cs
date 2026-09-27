using ArtCommission.Domain.Common;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Auction;

/// <summary>
/// Phiên đấu giá một tranh đã có chủ (UC32–UC35).
///
/// Bất biến nghiệp vụ:
///   - <see cref="CurrentPrice"/>= <see cref="StartPrice"/> trước bid đầu tiên, sau đó bằng bid dẫn đầu.
///   - Chỉ phiên <see cref="AuctionStatus.Scheduled"/> và CHƯA có bid mới được sửa/xoá.
///   - Một phiên tối đa MỘT bid dẫn đầu (<c>BidStatus.Leading</c>).
///   - <see cref="SettledAt"/> khác null là dấu hiệu đã chốt ⇒ settle phải idempotent.
/// </summary>
public class Auction : BaseEntity
{
    public Guid ArtworkId { get; set; }

    /// <summary>Người bán — phải là chủ sở hữu hiện tại của tranh.</summary>
    public Guid SellerId { get; set; }

    public AuctionType AuctionType { get; set; } = AuctionType.Standard;

    /// <summary>Giá khởi điểm. Mọi bid phải ≥ giá này.</summary>
    public decimal StartPrice { get; set; }

    /// <summary>Giá sàn bí mật. Dưới giá này thì không chốt bán (không lộ ra API).</summary>
    public decimal? ReservePrice { get; set; }

    /// <summary>Bước giá tối thiểu giữa 2 lượt bid liên tiếp.</summary>
    public decimal BidStep { get; set; }

    /// <summary>Giá mua ngay. Có giá trị thì được phép gọi buy-now.</summary>
    public decimal? BuyNowPrice { get; set; }

    /// <summary>Giá đang dẫn — nguồn sự thật cho điều kiện bid hợp lệ.</summary>
    public decimal CurrentPrice { get; set; }

    public int BidCount { get; set; }

    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }

    public AuctionStatus Status { get; set; } = AuctionStatus.Scheduled;

    /// <summary>Người thắng sau khi chốt. Null khi chưa chốt hoặc không có bid hợp lệ.</summary>
    public Guid? WinnerId { get; set; }

    /// <summary>Giá chốt cuối cùng (có thể là buy-now). Null khi chưa chốt.</summary>
    public decimal? FinalPrice { get; set; }

    public DateTimeOffset? SettledAt { get; set; }

    /// <summary>Hạn thanh toán của winner sau khi chốt.</summary>
    public DateTimeOffset? PaymentDeadline { get; set; }

    /// <summary>
    /// Lý do huỷ phiên. Bắt buộc có giá trị khi <see cref="Status"/> là Cancelled hoặc Expired
    /// để tra soát về sau.
    /// </summary>
    public string? CancelReason { get; set; }

    // Navigation
    public Artwork? Artwork { get; set; }
    public ICollection<Bid> Bids { get; set; } = [];
    public ICollection<AuctionWatch> Watches { get; set; } = [];
}
