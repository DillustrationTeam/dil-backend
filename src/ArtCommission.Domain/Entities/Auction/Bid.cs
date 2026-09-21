using ArtCommission.Domain.Common;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Auction;

/// <summary>
/// Một lượt đặt giá trong phiên đấu giá.
///
/// Tiền cọc: khi bid thành công, <see cref="HoldAmount"/> được chuyển từ
/// <c>Wallet.Balance</c> sang <c>Wallet.LockedBalance</c> trong CÙNG transaction ACID
/// với việc ghi bid (xem <c>BidOnAuctionCommand</c>).
///
/// Bất biến: (<see cref="BidStatus"/>, <see cref="HoldStatus"/>) phải đi cùng nhau —
///   Leading ⇒ Held · Outbid/Lost/Cancelled/Expired ⇒ Refunded · Won ⇒ Held hoặc Released.
/// </summary>
public class Bid : BaseEntity
{
    public Guid AuctionId { get; set; }
    public Guid BidderId { get; set; }

    /// <summary>Số tiền đặt giá.</summary>
    public decimal Amount { get; set; }

    /// <summary>Số tiền đã khoá làm cọc cho lượt bid này.</summary>
    public decimal HoldAmount { get; set; }

    public BidStatus Status { get; set; } = BidStatus.Leading;
    public HoldStatus HoldStatus { get; set; } = HoldStatus.None;

    /// <summary>True nếu đây là bid tự động (max auto-bid) thay vì bid thủ công.</summary>
    public bool IsAuto { get; set; }

    /// <summary>Trần giá của auto-bid. Null với bid thủ công.</summary>
    public decimal? MaxAutoBid { get; set; }

    public DateTimeOffset PlacedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public Auction? Auction { get; set; }
    public ApplicationUser? Bidder { get; set; }
}
