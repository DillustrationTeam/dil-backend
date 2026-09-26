namespace ArtCommission.Application.Admin.DTOs;

/// <summary>
/// Payload gửi lên khi Admin thi hành chế tài xử phạt (SCR-23 / UC31).
/// </summary>
public record ApplyUserSanctionRequest
{
    /// <summary>
    /// Loại chế tài: "Warn" (Cảnh cáo), "Suspend" (Đình chỉ có thời hạn), "Ban" (Khóa vĩnh viễn), "Unban" (Mở khóa).
    /// </summary>
    public string ActionType { get; init; } = string.Empty;

    /// <summary>
    /// Lý do xử phạt vi phạm chính sách / điều khoản (Bắt buộc).
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Số ngày đình chỉ (Bắt buộc nếu ActionType = "Suspend", ví dụ: 3, 7, 30 ngày).
    /// </summary>
    public int? DurationDays { get; init; }
}

/// <summary>
/// Payload cập nhật phân quyền Roles cho tài khoản người dùng (SCR-23 / UC31).
/// </summary>
public record UpdateUserRolesRequest
{
    /// <summary>
    /// Danh sách các vai trò mới (Administrator, Moderator, Creator, Client).
    /// </summary>
    public List<string> Roles { get; init; } = new();
}
