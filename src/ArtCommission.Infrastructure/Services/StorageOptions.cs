namespace ArtCommission.Infrastructure.Services;

/// <summary>
/// Cấu hình kho file (Cloudinary/S3 hoặc CDN nội bộ). Bind từ section "Storage".
///
/// <see cref="AllowedDownloadHosts"/> là allowlist host được phép sinh link tải.
/// VÌ SAO cần: handler tải file gốc nhận URL từ dữ liệu tranh. Nếu không giới hạn host,
/// một URL bị sửa trong DB sẽ biến endpoint tải thành công cụ gọi ra host bất kỳ (SSRF).
/// </summary>
public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Danh sách host được phép. Rỗng = cho phép mọi host (chỉ dùng khi dev).
    /// Khai báo trong appsettings: Storage:AllowedDownloadHosts:0 = "res.cloudinary.com"
    /// </summary>
    public List<string> AllowedDownloadHosts { get; set; } = [];

    /// <summary>Thời gian hiệu lực mặc định của link tải (phút).</summary>
    public int DownloadUrlLifetimeMinutes { get; set; } = 15;

    /// <summary>
    /// Khoá ký link tải. Chỉ đặt khi có CDN tự kiểm chữ ký; để trống thì link
    /// chỉ mang tham số hết hạn (xem ghi chú trong <c>FileStorageService</c>).
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Base CDN dùng khi cần chuyển URL gốc sang URL có kiểm soát.</summary>
    public string? CdnBaseUrl { get; set; }

    /// <summary>
    /// Thư mục gốc lưu file khi chưa nối Cloudinary/S3.
    /// Tương đối so với thư mục chạy của API (thường là <c>src/ArtCommission.API</c>).
    /// </summary>
    public string LocalRootPath { get; set; } = "uploads";

    /// <summary>
    /// Tiền tố URL công khai tương ứng với <see cref="LocalRootPath"/>.
    /// Phải khớp với cấu hình static files của API.
    /// </summary>
    public string PublicBasePath { get; set; } = "/uploads";
}
