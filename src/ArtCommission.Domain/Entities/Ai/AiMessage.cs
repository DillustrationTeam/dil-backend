using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Ai;

/// <summary>
/// Một lượt tin nhắn trong phiên hội thoại AI (UC44).
///
/// <see cref="FeedbackHelpful"/> / <see cref="FeedbackNote"/> là yêu cầu schema bổ sung:
/// UC44 cần người dùng đánh giá câu trả lời để cải thiện bot, nếu không có cột này
/// thì endpoint <c>POST /ai/messages/{id}/feedback</c> không lưu được gì.
///
/// <see cref="TokenCount"/> lưu số token đã dùng để theo dõi chi phí gọi Gemini.
/// </summary>
public class AiMessage : BaseEntity
{
    public Guid ConversationId { get; set; }

    public AiMessageRole Role { get; set; } = AiMessageRole.User;

    public string Content { get; set; } = string.Empty;

    /// <summary>Số token của lượt này (null nếu nhà cung cấp không trả về).</summary>
    public int? TokenCount { get; set; }

    // ------------------------------------------------------------------
    // Feedback người dùng (UC44)
    // ------------------------------------------------------------------

    /// <summary>True = hữu ích, false = không hữu ích. Null = chưa đánh giá.</summary>
    public bool? FeedbackHelpful { get; set; }

    public string? FeedbackNote { get; set; }

    public DateTimeOffset? FeedbackAt { get; set; }

    /// <summary>Model đã sinh ra câu trả lời — phục vụ so sánh chất lượng giữa các phiên bản.</summary>
    public string? ModelVersion { get; set; }

    // Navigation
    public AiConversation? Conversation { get; set; }
}
