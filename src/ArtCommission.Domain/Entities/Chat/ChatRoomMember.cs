using ArtCommission.Domain.Common;
using ArtCommission.Domain.Entities.Identity;

namespace ArtCommission.Domain.Entities.Chat;

/// <summary>
/// Thành viên của một phòng chat. Giữ <see cref="LastReadAt"/> để tính số tin chưa đọc
/// và để ghi nhận "đã đọc tới đâu" khi user rời phòng.
///
/// Bất biến: mỗi cặp (RoomId, UserId) tối đa một dòng — ép bằng unique index.
/// </summary>
public class ChatRoomMember : BaseEntity
{
    public Guid RoomId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Vai trò trong phòng: "Client", "Creator", "Moderator".</summary>
    public string MemberRole { get; set; } = "Client";

    /// <summary>Thời điểm đọc tin nhắn cuối cùng. Null nghĩa là chưa đọc gì.</summary>
    public DateTimeOffset? LastReadAt { get; set; }

    /// <summary>True nếu thành viên đã rời phòng (giữ lại để tra soát lịch sử).</summary>
    public bool HasLeft { get; set; }

    // Navigation
    public ChatRoom? Room { get; set; }
    public ApplicationUser? User { get; set; }
}
