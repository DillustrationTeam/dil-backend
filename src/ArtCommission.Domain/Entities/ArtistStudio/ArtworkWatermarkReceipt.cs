using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.ArtistStudio;

/// <summary>
/// Thực thể lưu trữ "Biên lai thuỷ vân số" (Forensic Watermark Receipt).
/// Sử dụng cơ chế Tokenization để truy vết nguồn gốc khi tranh bị rò rỉ hoặc chụp màn hình
/// mà không làm lộ trực tiếp thông tin nhạy cảm của người xem trên ảnh.
/// </summary>
public class ArtworkWatermarkReceipt : BaseEntity
{
    [Required]
    public Guid ArtworkId { get; set; }

    [Required]
    [MaxLength(64)]
    public string ReceiptToken { get; set; } = string.Empty;

    /// <summary>
    /// Giá trị số 64-bit nhúng vào dải tần số DWT của ảnh (nếu áp dụng Invisible Watermark).
    /// </summary>
    public long? NumericPayload { get; set; }

    /// <summary>
    /// ID người dùng mở xem tác phẩm (null nếu là khách vãng lai).
    /// </summary>
    public Guid? ViewerUserId { get; set; }

    [MaxLength(256)]
    public string? ViewerEmail { get; set; }

    [MaxLength(256)]
    public string? ViewerName { get; set; }

    [Required]
    [MaxLength(64)]
    public string IpAddress { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    [MaxLength(128)]
    public string? SessionId { get; set; }

    [MaxLength(50)]
    public string WatermarkType { get; set; } = "DwtForensic";

    [MaxLength(50)]
    public string Status { get; set; } = "Active";

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ArtworkId))]
    public Artwork? Artwork { get; set; }
}
