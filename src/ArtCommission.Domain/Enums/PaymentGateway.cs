namespace ArtCommission.Domain.Enums;

/// <summary>
/// Cổng thanh toán. Hiện chỉ PayOS được triển khai (UC48).
///
/// Muốn thêm cổng khác (VNPAY, Stripe...): thêm giá trị vào enum này,
/// thêm tên vào <see cref="PaymentGatewayNames"/>, rồi viết một class
/// implement <c>IPaymentGateway</c> và đăng ký DI.
/// Handler KHÔNG phải sửa vì đã làm việc qua interface.
/// </summary>
public enum PaymentGateway
{
    PayOS
}

public static class PaymentGatewayNames
{
    public const string PayOS = nameof(PaymentGateway.PayOS);

    public static readonly string[] All = [PayOS];
}
