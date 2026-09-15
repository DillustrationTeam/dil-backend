namespace ArtCommission.Infrastructure.ExternalServices.PayOs;

/// <summary>
/// Cấu hình cổng payOS. Bind từ section "PayOS" trong configuration.
/// Secret (ApiKey, ChecksumKey) KHÔNG để trong appsettings.json —
/// dùng User Secrets khi dev, biến môi trường khi deploy
/// (PayOS__ClientId, PayOS__ApiKey, PayOS__ChecksumKey).
/// </summary>
public class PayOsOptions
{
    public const string SectionName = "PayOS";

    public string ClientId { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Khoá dùng để tạo/kiểm tra chữ ký HMAC-SHA256 của webhook.</summary>
    public string ChecksumKey { get; set; } = string.Empty;

    /// <summary>Thời gian chờ mỗi request tới payOS (ms).</summary>
    public int TimeoutMs { get; set; } = 30_000;

    /// <summary>Số lần thử lại tối đa khi payOS trả lỗi tạm thời.</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// Đơn vị tiền tệ. payOS chỉ hỗ trợ VND — giữ ở đây để tường minh,
    /// không cho phép cấu hình sang loại khác.
    /// </summary>
    public string Currency => "VND";

    /// <summary>Kiểm tra cấu hình đã đủ 3 khoá bắt buộc chưa.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(ChecksumKey);
}
