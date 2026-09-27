using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Auction;

/// <summary>
/// Bản ghi bàn giao file gốc của tranh cho winner
/// (UC35 · POST /auctions/{auctionId}/settlement/download-url).
///
/// VÌ SAO cần entity riêng dù <c>Artwork.ImageUrl</c> đã có:
///   - URL công khai của tranh là bản đã watermark để bảo hộ tác quyền,
///     KHÔNG được dùng để giao file gốc.
///   - Cần bảng riêng đánh dấu "đã bàn giao cho ai, lúc nào, hết hạn tải khi nào"
///     — nếu chỉ dựa vào trạng thái thanh toán thì không truy vết được.
///   - FK <see cref="AuctionId"/> là yêu cầu schema đã ghi trong API_List_An.md.
/// </summary>
public class Deliverable : BaseEntity
{
    public Guid AuctionId { get; set; }

    /// <summary>Người được nhận file gốc — winner đã thanh toán.</summary>
    public Guid RecipientId { get; set; }

    /// <summary>Tên file hiển thị khi tải về.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// URL file gốc. Không trả trực tiếp cho client — chỉ dùng để sinh
    /// presigned URL có hạn trong handler download-url.
    /// </summary>
    public string FileUrl { get; set; } = string.Empty;

    public string? MimeType { get; set; }
    public long? FileSizeBytes { get; set; }

    /// <summary>Đã bàn giao (winner đã tải hoặc đã cấp link) hay chưa.</summary>
    public bool IsDelivered { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }

    /// <summary>Số lần winner đã lấy link tải — phục vụ phát hiện lạm dụng.</summary>
    public int DownloadCount { get; set; }

    // Navigation
    public Auction? Auction { get; set; }
}
