using ArtCommission.Domain.Common;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Chat;

/// <summary>
/// Tin nhắn trong phòng Workroom (UC43).
///
/// Giữ lại <see cref="CommissionId"/> để tương thích dữ liệu cũ, nhưng đường đi
/// chính từ nay là <see cref="RoomId"/> (phòng chat có thể gắn auction hoặc hỗ trợ,
/// không chỉ commission).
///
/// Cụm trường dịch (<see cref="SourceLang"/>, <see cref="TargetLang"/>,
/// <see cref="TranslatedBody"/>, <see cref="TranslationStatus"/>) là yêu cầu schema
/// bổ sung cho UC43 — thiếu chúng thì không retry được khi gọi AI dịch lỗi.
/// </summary>
public class Message : BaseEntity
{
    /// <summary>Phòng chat chứa tin nhắn. Null với dữ liệu cũ tạo trước khi có ChatRoom.</summary>
    public Guid? RoomId { get; set; }

    /// <summary>Đơn đặt vẽ liên quan (giữ để tương thích ngược).</summary>
    public Guid? CommissionId { get; set; }

    public Guid SenderId { get; set; }

    /// <summary>
    /// Loại nội dung: "Text", "Image", "File", "SystemEvent", "RevisionNotice".
    ///
    /// GIỮ KIỂU STRING (không đổi sang enum) vì module Commission/Dispute đang đọc
    /// thẳng trường này và đổ vào DTO kiểu string. Đổi sang enum sẽ làm vỡ module khác.
    /// Giá trị hợp lệ được kiểm trong handler bằng <see cref="MessageTypes"/>.
    /// </summary>
    public string MessageType { get; set; } = MessageTypes.Text;

    /// <summary>Nội dung gốc do người gửi nhập.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>URL file đính kèm nhanh (tương thích ngược với bản cũ).</summary>
    public string? AttachmentUrl { get; set; }

    // ------------------------------------------------------------------
    // Dịch tự động (AI Real-time Translation)
    // ------------------------------------------------------------------

    /// <summary>Ngôn ngữ gốc của <see cref="Body"/>, ví dụ "vi", "en".</summary>
    public string? SourceLang { get; set; }

    /// <summary>Ngôn ngữ đích đã dịch.</summary>
    public string? TargetLang { get; set; }

    /// <summary>Bản dịch. Có giá trị ⇒ không gọi AI lại cho cùng cặp ngôn ngữ.</summary>
    public string? TranslatedBody { get; set; }

    public TranslationStatus TranslationStatus { get; set; } = TranslationStatus.NotRequested;

    /// <summary>Lý do dịch lỗi — phục vụ retry và tra soát.</summary>
    public string? TranslationError { get; set; }

    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Đã đọc bởi người nhận hay chưa (cờ nhanh; nguồn sự thật là ChatRoomMember.LastReadAt).</summary>
    public bool IsRead { get; set; }

    // Navigation
    public ChatRoom? Room { get; set; }
    public ApplicationUser? Sender { get; set; }
    public ICollection<MessageAttachment> Attachments { get; set; } = [];
}
