using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Chat;

/// <summary>
/// File/ảnh đính kèm trong một tin nhắn Workroom.
/// Một tin nhắn có thể có nhiều đính kèm (dù UI thường gửi 1 file mỗi lần).
///
/// VÌ SAO tách bảng thay vì nhồi mảng vào <see cref="Message"/>:
/// cần truy vấn "file nào thuộc tin nào" và lưu metadata (mime, size) mà không
/// phải parse chuỗi JSON trong cột.
/// </summary>
public class MessageAttachment : BaseEntity
{
    /// <summary>Tin nhắn chứa file này.</summary>
    public Guid MessageId { get; set; }

    /// <summary>Người upload — để kiểm tra quyền xoá.</summary>
    public Guid UploadedBy { get; set; }

    /// <summary>URL công khai trên Cloudinary/S3.</summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>Tên file gốc do người dùng chọn.</summary>
    public string? FileName { get; set; }

    public string MimeType { get; set; } = "application/octet-stream";
    public long FileSize { get; set; }

    // Navigation
    public Message? Message { get; set; }
}
