using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Notifications;

/// <summary>
/// Thông báo gửi tới MỘT người dùng (UC45).
///
/// Không dùng chung entity với module khác: mỗi thông báo thuộc đúng 1 <see cref="UserId"/>
/// và được đẩy real-time vào SignalR group <c>user-{UserId}</c>.
///
/// <see cref="DedupKey"/> là chốt chống gửi trùng — mọi nơi phát thông báo PHẢI
/// truyền khoá này (ví dụ <c>OutbidAlert:{auctionId}:{oderUserId}:{bidId}</c>) để
/// job/retry chạy lại không tạo thêm thông báo.
/// </summary>
public class Notification : BaseEntity
{
    /// <summary>Người nhận.</summary>
    public Guid UserId { get; set; }

    public NotificationType NotificationType { get; set; }

    /// <summary>Tiêu đề ngắn hiển thị trên drawer.</summary>
    public string NotificationTitle { get; set; } = string.Empty;

    /// <summary>Nội dung chi tiết.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Loại đối tượng được nhắc tới: "Auction", "Commission", "Milestone", "PayoutRequest"...</summary>
    public string? RefType { get; set; }

    /// <summary>Id đối tượng tương ứng <see cref="RefType"/> — dùng để điều hướng khi bấm vào.</summary>
    public Guid? RefId { get; set; }

    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;

    public bool IsRead { get; set; }

    public DateTimeOffset? ReadAt { get; set; }

    /// <summary>
    /// Khoá chống trùng — unique theo <c>(UserId, DedupKey)</c>.
    /// Null = không chống trùng (thông báo tự do).
    /// </summary>
    public string? DedupKey { get; set; }

    /// <summary>Thời điểm gửi thành công qua kênh ngoài (Email/Push). Null với InApp.</summary>
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>Lý do gửi lỗi qua kênh ngoài — phục vụ retry.</summary>
    public string? FailedReason { get; set; }

    // Navigation
    public ArtCommission.Domain.Entities.Identity.ApplicationUser? User { get; set; }
}
