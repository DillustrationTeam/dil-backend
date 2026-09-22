using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Auction;

/// <summary>
/// Bản ghi escrow cho một giao dịch đấu giá — dấu vết tiền thắng phiên
/// (UC35 · POST /auctions/{auctionId}/settle).
///
/// VÌ SAO cần entity riêng thay vì chỉ dùng <c>WalletTransaction</c>:
///   - <c>WalletTransaction</c> là sổ cái ví của MỘT người; không biểu diễn được
///     quan hệ người-trả → người-nhận của một giao dịch.
///   - Escrow cần trạng thái riêng (<see cref="EscrowStatus"/>) để biết tiền
///     đang giữ, đã giải ngân, đã hoàn, hay đang tranh chấp.
///   - Là chỗ để gắn <see cref="AuctionId"/> — FK mà API_List_An.md yêu cầu bổ sung.
///
/// Bất biến: <see cref="ReleasedAmount"/> + <see cref="RefundedAmount"/> ≤ <see cref="Amount"/>.
/// </summary>
public class EscrowTransaction : BaseEntity
{
    public Guid AuctionId { get; set; }

    /// <summary>Người trả tiền — bidder thắng phiên.</summary>
    public Guid PayerId { get; set; }

    /// <summary>Người nhận tiền — seller của phiên.</summary>
    public Guid PayeeId { get; set; }

    /// <summary>Tổng số tiền được giữ cho giao dịch này.</summary>
    public decimal Amount { get; set; }

    /// <summary>Đã giải ngân cho payee.</summary>
    public decimal ReleasedAmount { get; set; }

    /// <summary>Đã hoàn lại cho payer.</summary>
    public decimal RefundedAmount { get; set; }

    /// <summary>
    /// Phí nền tảng đã trừ trong giao dịch này.
    /// Lưu riêng thay vì suy ra từ <c>Amount − ReleasedAmount</c>: cách suy ra sẽ
    /// sai ngay khi có hoàn tiền một phần, và không đối soát được phí.
    /// </summary>
    public decimal FeeAmount { get; set; }

    public EscrowStatus Status { get; set; } = EscrowStatus.Pending;

    public DateTimeOffset? ReleasedAt { get; set; }
    public DateTimeOffset? RefundedAt { get; set; }

    /// <summary>Ghi chú tra soát (lý do hoàn, mã giao dịch ngân hàng...).</summary>
    public string? Note { get; set; }

    // Navigation
    public Auction? Auction { get; set; }
}
