namespace ArtCommission.Domain.Enums;

/// <summary>
/// Vòng đời của một đơn nạp tiền.
/// Pending  : vừa tạo, đã sinh link thanh toán, chờ người dùng trả tiền
/// Paid     : cổng xác nhận qua webhook, tiền đã cộng vào ví
/// Failed   : thất bại (sai chữ ký / số tiền lệch / cổng báo lỗi)
/// Expired  : link thanh toán hết hạn mà chưa trả
/// Cancelled: người dùng hoặc hệ thống huỷ trước khi trả
/// </summary>
public enum PaymentOrderStatus
{
    Pending,
    Paid,
    Failed,
    Expired,
    Cancelled
}

public static class PaymentOrderStatusNames
{
    public const string Pending = nameof(PaymentOrderStatus.Pending);
    public const string Paid = nameof(PaymentOrderStatus.Paid);
    public const string Failed = nameof(PaymentOrderStatus.Failed);
    public const string Expired = nameof(PaymentOrderStatus.Expired);
    public const string Cancelled = nameof(PaymentOrderStatus.Cancelled);

    public static readonly string[] All = [Pending, Paid, Failed, Expired, Cancelled];
}
