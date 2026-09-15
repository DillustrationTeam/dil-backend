using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Payment;

/// <summary>
/// Đơn nạp tiền vào ví sàn qua cổng thanh toán (UC48 — POST /api/v1/payments/orders).
/// </summary>
public class PaymentOrder : BaseEntity
{
    /// <summary>Mã hiển thị cho người dùng và dùng làm nội dung chuyển khoản.</summary>
    public string OrderRef { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    /// <summary>Ví nhận tiền khi thanh toán thành công.</summary>
    public Guid WalletId { get; set; }

    /// <summary>Số tiền nạp — VND, luôn dương.</summary>
    public decimal Amount { get; set; }

    public PaymentGateway Gateway { get; set; }

    public PaymentOrderStatus Status { get; set; } = PaymentOrderStatus.Pending;

    /// <summary>
    /// Số gửi sang PayOS. PayOS yêu cầu long ≤ 9.007.199.254.740.991
    /// (giới hạn JS safe integer), nên KHÔNG dùng Guid làm order code.
    /// </summary>
    public long PayOsOrderCode { get; set; }

    /// <summary>Id link thanh toán do payOS trả về (dùng để đối chiếu trạng thái).</summary>
    public string? PaymentLinkId { get; set; }

    /// <summary>URL trang thanh toán payOS trả về.</summary>
    public string? CheckoutUrl { get; set; }

    /// <summary>Chuỗi QR (VietQR) payOS trả về.</summary>
    public string? QrCode { get; set; }

    /// <summary>
    /// Mã giao dịch thật của cổng (webhook `reference` / `transaction_ref`).
    /// Là IDEMPOTENCY KEY — unique khi có giá trị, chống cộng tiền 2 lần.
    /// </summary>
    public string? TransactionRef { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    /// <summary>Thời điểm link thanh toán hết hạn.</summary>
    public DateTimeOffset? ExpiredAt { get; set; }

    /// <summary>Lý do thất bại / huỷ — phục vụ tra soát.</summary>
    public string? FailureReason { get; set; }

    // Navigation
    public Wallet? Wallet { get; set; }
}
