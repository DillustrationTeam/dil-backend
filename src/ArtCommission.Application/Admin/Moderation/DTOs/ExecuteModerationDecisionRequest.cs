namespace ArtCommission.Application.Admin.Moderation.DTOs;

/// <summary>
/// Payload gửi lên khi Moderator/Admin ra quyết định kiểm duyệt tác phẩm (SCR-20).
/// </summary>
public record ExecuteModerationDecisionRequest
{
    /// <summary>
    /// Hành động kiểm duyệt: "approve" (Duyệt), "reject" (Từ chối & xóa), "hide" (Ẩn khỏi gợi ý), "flag_ai" (Gắn cờ tranh AI).
    /// </summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// Ghi chú kiểm duyệt nội bộ / Lý do quyết định.
    /// </summary>
    public string? ModerationNote { get; init; }
}
