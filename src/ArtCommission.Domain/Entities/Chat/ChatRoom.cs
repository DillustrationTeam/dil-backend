using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Chat;

/// <summary>
/// Phòng chat Workroom (UC43) — nơi Client và Creator trao đổi trong một commission,
/// hoặc người mua và người bán trao đổi về một phiên đấu giá.
///
/// VÌ SAO tách khỏi <c>Commission</c>: phòng chat còn dùng cho phiên đấu giá và phòng
/// hỗ trợ, nên không thể buộc mọi phòng đều có CommissionId.
/// </summary>
public class ChatRoom : BaseEntity
{
    public ChatRoomType RoomType { get; set; } = ChatRoomType.Commission;

    /// <summary>Tiêu đề hiển thị trên danh sách phòng.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Đơn đặt vẽ gắn với phòng (khi <see cref="RoomType"/> = Commission).</summary>
    public Guid? CommissionId { get; set; }

    /// <summary>Phiên đấu giá gắn với phòng (khi <see cref="RoomType"/> = Auction).</summary>
    public Guid? AuctionId { get; set; }

    /// <summary>Thời điểm có tin nhắn gần nhất — dùng sắp xếp danh sách phòng.</summary>
    public DateTimeOffset? LastMessageAt { get; set; }

    /// <summary>Phòng đã bị khoá (đơn hoàn tất / tranh chấp) — chỉ đọc, không gửi mới.</summary>
    public bool IsLocked { get; set; }

    // Navigation
    public ICollection<ChatRoomMember> Members { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];
}
