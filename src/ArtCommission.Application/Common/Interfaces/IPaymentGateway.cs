namespace ArtCommission.Application.Common.Interfaces;

using System.Text.Json;

/// <summary>
/// Lớp trừu tượng cho cổng thanh toán.
/// Handler chỉ làm việc với interface này, không biết SDK cụ thể là PayOS/VNPAY/MoMo,
/// nên thêm cổng mới không phải sửa handler.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Tên cổng — khớp với <see cref="ArtCommission.Domain.Enums.PaymentGateway"/>.</summary>
    string GatewayName { get; }

    /// <summary>Tạo link thanh toán tại cổng.</summary>
    Task<PaymentLinkResult> CreatePaymentLinkAsync(
        PaymentLinkRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Đối chiếu trạng thái link thanh toán với cổng (dùng cho polling và job đối soát).</summary>
    Task<PaymentLinkInfo> GetPaymentLinkAsync(
        long orderCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xác thực chữ ký webhook và trả dữ liệu đã kiểm.
    /// Ném <see cref="PaymentSignatureException"/> nếu chữ ký sai — caller map thành HTTP 400.
    /// </summary>
    /// <param name="webhookBody">Body webhook đã được deserialize thành JSON.</param>
    Task<PaymentWebhookData> VerifyWebhookAsync(
        JsonElement webhookBody,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đăng ký webhook URL với cổng (PayOS sẽ test endpoint trước khi nhận).
    /// </summary>
    Task<bool> ConfirmWebhookAsync(
        string webhookUrl,
        CancellationToken cancellationToken = default);
}

/// <summary>Chữ ký webhook không hợp lệ — map thành HTTP 400, không lộ chi tiết ra client.</summary>
public class PaymentSignatureException : Exception
{
    public PaymentSignatureException(string message) : base(message) { }

    public PaymentSignatureException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Lỗi nghiệp vụ từ cổng thanh toán (cổng từ chối, sai tham số...).</summary>
public class PaymentGatewayException : Exception
{
    public PaymentGatewayException(string message) : base(message) { }

    public PaymentGatewayException(string message, Exception inner) : base(message, inner) { }
}
