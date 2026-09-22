namespace ArtCommission.Application.Common.Interfaces;

/// <summary>
/// Cổng sinh URL tải file có hạn dùng.
///
/// VÌ SAO không trả thẳng <c>FileUrl</c> của file gốc:
/// file gốc là tài sản bản quyền — nếu URL công khai thì chỉ cần biết đường dẫn là
/// tải được mãi mãi, không kiểm soát được ai đã tải. URL có hạn + chữ ký buộc
/// mọi lượt tải phải đi qua server để kiểm quyền.
///
/// Cài đặt thật (Cloudinary/S3) ở tầng Infrastructure.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Sinh URL tải có hạn cho một file.
    /// </summary>
    /// <param name="fileUrl">URL gốc của file.</param>
    /// <param name="validFor">Thời gian hiệu lực tính từ bây giờ.</param>
    Task<PresignedDownload> CreateDownloadUrlAsync(
        string fileUrl,
        string fileName,
        TimeSpan validFor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lưu nội dung file lên kho và trả về URL công khai để truy cập.
    ///
    /// Bắt buộc: tên file phải được làm sạch và thêm tiền tố duy nhất trước khi ghi,
    /// nếu không người dùng có thể ghi đè file của người khác hoặc thoát khỏi thư mục
    /// đích bằng tên kiểu <c>../../appsettings.json</c>.
    /// </summary>
    /// <param name="folder">
    /// Thư mục con logic (ví dụ <c>chat/{roomId}</c>). Giá trị này do server sinh,
    /// KHÔNG lấy trực tiếp từ client.
    /// </param>
    Task<string> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default);
}

/// <summary>URL tải có hạn kèm thời điểm hết hiệu lực.</summary>
public sealed record PresignedDownload(string Url, DateTimeOffset ExpiresAt);
