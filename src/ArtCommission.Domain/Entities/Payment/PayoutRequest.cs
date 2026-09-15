using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Payment;

/// <summary>
/// Yêu cầu rút tiền của Creator về tài khoản ngân hàng (UC49 — POST /api/v1/payout-requests).
/// Tiền bị trừ khỏi ví NGAY khi tạo yêu cầu, hoàn lại nếu Admin từ chối.
/// </summary>
public class PayoutRequest : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>Ví bị trừ tiền.</summary>
    public Guid WalletId { get; set; }

    /// <summary>Tài khoản ngân hàng nhận tiền.</summary>
    public Guid BankAccountId { get; set; }

    /// <summary>Số tiền yêu cầu rút — VND.</summary>
    public decimal Amount { get; set; }

    public PayoutStatus Status { get; set; } = PayoutStatus.Pending;

    /// <summary>Ghi chú của Creator khi gửi yêu cầu.</summary>
    public string? PayoutNote { get; set; }

    /// <summary>Admin duyệt / từ chối.</summary>
    public Guid? ProcessedBy { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>Mã giao dịch chuyển khoản thật do Admin nhập khi duyệt.</summary>
    public string? TransactionRef { get; set; }

    /// <summary>Lý do từ chối — bắt buộc khi Status = Rejected.</summary>
    public string? RejectReason { get; set; }

    /// <summary>
    /// Snapshot thông tin ngân hàng tại thời điểm gửi yêu cầu.
    /// Cần thiết vì Creator có thể sửa/xoá <see cref="BankAccount"/> sau đó,
    /// nhưng lịch sử rút tiền phải giữ đúng thông tin đã dùng.
    /// </summary>
    public string? BankAccountSnapshot { get; set; }

    // Navigation
    public BankAccount? BankAccount { get; set; }
}
