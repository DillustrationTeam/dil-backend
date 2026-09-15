namespace ArtCommission.Domain.Enums;

/// <summary>
/// Cổng thanh toán được hỗ trợ. Hiện chỉ PayOS được triển khai (UC48).
/// </summary>
public enum PaymentGateway
{
    PayOS,
    VnPay,
    Stripe
}

public static class PaymentGatewayNames
{
    public const string PayOS = nameof(PaymentGateway.PayOS);
    public const string VnPay = nameof(PaymentGateway.VnPay);
    public const string Stripe = nameof(PaymentGateway.Stripe);

    public static readonly string[] All = [PayOS, VnPay, Stripe];
}
