using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Auction.Common;

/// <summary>
/// Tập giá trị <c>WalletTransaction.RefType</c> của module đấu giá.
///
/// VÌ SAO PHẢI TÁCH NHIỀU GIÁ TRỊ THAY VÌ DÙNG MỘT:
/// Bảng sổ cái có unique index <c>UX_WalletTransaction_Ref</c> trên
/// <c>(RefType, RefId, Type)</c> với filter <c>RefId IS NOT NULL</c>.
/// Index này KHÔNG bao gồm WalletId — nghĩa là một cặp (RefType, RefId, Type)
/// chỉ được tồn tại ĐÚNG MỘT dòng trên toàn bộ sổ cái.
///
/// Hệ quả nếu dùng chung <c>RefType = "Auction"</c> và <c>RefId = auctionId</c>:
///   - bid thứ hai của cùng phiên ghi (Auction, auctionId, EscrowHold) lần nữa
///     ⇒ vi phạm unique index ⇒ TOÀN BỘ thao tác đặt giá thứ hai thất bại.
///   - tiền về người bán và tiền trừ người mua cùng là (Auction, auctionId, EscrowRelease)
///     ⇒ chốt phiên thất bại.
///
/// Vì vậy mỗi LOẠI nghiệp vụ tiền dùng một RefType riêng, và RefId là định danh
/// hẹp nhất của nghiệp vụ đó:
///   - cọc theo từng lượt bid  ⇒ RefId = Bid.Id
///   - tiền ở cấp phiên        ⇒ RefId = Auction.Id
/// Nhờ vậy mỗi dòng sổ cái là duy nhất và index vẫn giữ được tác dụng chống ghi trùng.
/// </summary>
public static class AuctionRefTypes
{
    /// <summary>Cọc của một lượt bid: giữ (EscrowHold), hoàn (RefundFromHold), giải ngân (EscrowRelease).</summary>
    public const string BidDeposit = "AuctionBidDeposit";

    /// <summary>Trừ ví người mua khi mua ngay.</summary>
    public const string BuyNowPayment = "AuctionBuyNowPayment";

    /// <summary>Bù phần cọc còn thiếu so với giá chốt (khi cọc theo tỉ lệ).</summary>
    public const string BidShortfall = "AuctionBidShortfall";

    /// <summary>Tiền về người bán và phí nền tảng ở cấp phiên.</summary>
    public const string Settlement = "AuctionSettlement";

    public static readonly string[] All =
        [BidDeposit, BuyNowPayment, BidShortfall, Settlement];

    /// <summary>
    /// Kiểm tra một RefType có thuộc module đấu giá không.
    /// Dùng cho module doanh thu để phân loại nguồn Commission/Auction.
    /// </summary>
    public static bool IsAuction(string? refType) =>
        refType is not null && Array.Exists(All, r => string.Equals(r, refType, StringComparison.OrdinalIgnoreCase));
}
