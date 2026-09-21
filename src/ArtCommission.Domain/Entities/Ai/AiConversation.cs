using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Ai;

/// <summary>
/// Một phiên hội thoại với Chatbot 24/7 (UC44).
///
/// <see cref="ContextType"/> + <see cref="ContextId"/> quyết định prompt hệ thống:
///   - General    → chỉ mô tả nền tảng.
///   - Commission → nạp trạng thái, milestone, escrow, deadline của đơn.
///   - Artwork    → nạp tiêu đề, phong cách, trạng thái kiểm duyệt của tranh.
///
/// Quan trọng: ngữ cảnh được nạp lại MỖI LƯỢT hỏi để câu trả lời phản ánh trạng thái
/// mới nhất, không đóng băng tại thời điểm tạo phiên.
/// </summary>
public class AiConversation : BaseEntity
{
    /// <summary>Chủ phiên hội thoại.</summary>
    public Guid UserId { get; set; }

    /// <summary>Tiêu đề phiên — lấy từ câu hỏi đầu tiên nếu client không truyền.</summary>
    public string Topic { get; set; } = string.Empty;

    public AiContextType ContextType { get; set; } = AiContextType.General;

    /// <summary>Id đối tượng ngữ cảnh. Null khi <see cref="ContextType"/> = General.</summary>
    public Guid? ContextId { get; set; }

    /// <summary>Thời điểm có tin nhắn gần nhất — dùng sắp xếp danh sách phiên.</summary>
    public DateTimeOffset? LastMessageAt { get; set; }

    public int MessageCount { get; set; }

    /// <summary>Phiên đã bị lưu trữ (ẩn khỏi danh sách mặc định).</summary>
    public bool IsArchived { get; set; }

    // Navigation
    public ICollection<AiMessage> Messages { get; set; } = [];
}
