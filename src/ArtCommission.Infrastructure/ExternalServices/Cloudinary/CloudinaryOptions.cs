namespace ArtCommission.Infrastructure.ExternalServices.Cloudinary;

/// <summary>
/// Cấu hình Cloudinary. Bind từ section "Cloudinary" trong configuration.
/// ApiSecret KHÔNG để trong appsettings.json — dùng User Secrets khi dev,
/// biến môi trường khi deploy (Cloudinary__ApiSecret).
/// </summary>
public class CloudinaryOptions
{
    public const string SectionName = "Cloudinary";

    public string CloudName { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Khoá dùng để ký chữ ký HMAC-SHA1 cho signed upload. Không bao giờ gửi cho client.</summary>
    public string ApiSecret { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(CloudName) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(ApiSecret);
}
