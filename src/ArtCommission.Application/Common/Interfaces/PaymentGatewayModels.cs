namespace ArtCommission.Application.Common.Interfaces;

/// <summary>
/// Dữ liệu cần để tạo một link thanh toán ở cổng.
/// Đây là DTO trung lập — không phụ thuộc SDK của cổng nào,
/// để sau này thêm VNPAY/MoMo không phải sửa handler.
/// </summary>
/// <param name="OrderCode">Mã đơn gửi sang cổng. PayOS yêu cầu long ≤ 9.007.199.254.740.991.</param>
/// <param name="Amount">Số tiền — VND.</param>
/// <param name="Description">Nội dung chuyển khoản hiển thị cho người dùng.</param>
/// <param name="ReturnUrl">URL quay về khi thanh toán thành công.</param>
/// <param name="CancelUrl">URL quay về khi người dùng huỷ.</param>
/// <param name="Items">Nội dung đơn hàng chi tiết (tuỳ chọn).</param>
/// <param name="BuyerName">Tên người mua (tuỳ chọn).</param>
/// <param name="BuyerEmail">Email người mua (tuỳ chọn).</param>
/// <param name="ExpiredAt">Thời điểm link hết hạn (tuỳ chọn).</param>
public record PaymentLinkRequest(
    long OrderCode,
    long Amount,
    string Description,
    string ReturnUrl,
    string CancelUrl,
    IReadOnlyList<PaymentLinkItem>? Items = null,
    string? BuyerName = null,
    string? BuyerEmail = null,
    DateTimeOffset? ExpiredAt = null
);

/// <summary>Một dòng hàng trong link thanh toán.</summary>
public record PaymentLinkItem(string Name, int Quantity, long Price);

/// <summary>Kết quả cổng trả về khi tạo link thanh toán.</summary>
/// <param name="PaymentLinkId">Id link thanh toán phía cổng.</param>
/// <param name="CheckoutUrl">URL trang thanh toán.</param>
/// <param name="QrCode">Chuỗi QR (VietQR).</param>
/// <param name="AccountNumber">Số tài khoản nhận tiền (có thể là số tài khoản ảo).</param>
/// <param name="AccountName">Tên chủ tài khoản nhận tiền.</param>
/// <param name="Bin">Mã BIN ngân hàng nhận tiền.</param>
/// <param name="Status">Trạng thái cổng trả về, giữ nguyên chuỗi gốc (VD: "PENDING").</param>
/// <param name="ExpiredAt">Thời điểm link hết hạn.</param>
public record PaymentLinkResult(
    string PaymentLinkId,
    string CheckoutUrl,
    string QrCode,
    string AccountNumber,
    string AccountName,
    string Bin,
    string Status,
    DateTimeOffset? ExpiredAt
);

/// <summary>Trạng thái link thanh toán khi đối chiếu lại với cổng.</summary>
/// <param name="OrderCode">Mã đơn phía cổng.</param>
/// <param name="Amount">Số tiền yêu cầu.</param>
/// <param name="AmountPaid">Số tiền đã trả.</param>
/// <param name="AmountRemaining">Số tiền còn thiếu.</param>
/// <param name="Status">Trạng thái chuỗi gốc: PENDING/PAID/CANCELLED/EXPIRED/UNDERPAID/PROCESSING/FAILED.</param>
/// <param name="PaymentLinkId">Id link thanh toán phía cổng.</param>
/// <param name="Transactions">Danh sách giao dịch cổng ghi nhận.</param>
public record PaymentLinkInfo(
    long OrderCode,
    long Amount,
    long AmountPaid,
    long AmountRemaining,
    string Status,
    string PaymentLinkId,
    IReadOnlyList<PaymentLinkTransactionInfo> Transactions
);

/// <summary>Một giao dịch cổng ghi nhận trên link thanh toán.</summary>
public record PaymentLinkTransactionInfo(
    string Reference,
    long Amount,
    string? AccountNumber,
    string? Description,
    string? TransactionDateTime
);

/// <summary>Dữ liệu webhook đã được xác thực chữ ký.</summary>
public record PaymentWebhookData(
    bool Success,
    string Code,
    string Description,
    long OrderCode,
    long Amount,
    string? Reference,
    string? PaymentLinkId,
    string? AccountNumber,
    string? TransactionDateTime,
    string? Currency,
    string? CounterAccountName,
    string? CounterAccountNumber
);
