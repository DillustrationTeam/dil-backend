using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Identity;

/// <summary>
/// Thực thể ghi nhận các chế tài xử phạt tài khoản người dùng (SCR-23 / UC31 / UserSanctions).
/// Hoạt động theo cơ chế append-only log lưu trữ lịch sử mọi quyết định Cảnh cáo, Đình chỉ, Ban, Mở khóa.
/// </summary>
public class UserSanction : BaseEntity
{
    public Guid UserId { get; set; }
    public string ActionType { get; set; } = string.Empty; // "Warn", "Suspend", "Ban", "Unban"
    public string Reason { get; set; } = string.Empty;
    public int? DurationDays { get; set; } // Số ngày đình chỉ nếu là Suspend
    public DateTimeOffset? ExpiresAt { get; set; } // Thời điểm hết hạn hiệu lực khóa
    public Guid ActionByAdminId { get; set; }

    // Navigation properties
    public ApplicationUser User { get; set; } = null!;
    public ApplicationUser ActionByAdmin { get; set; } = null!;
}

public static class SanctionActionTypes
{
    public const string Warn = "Warn";
    public const string Suspend = "Suspend";
    public const string Ban = "Ban";
    public const string Unban = "Unban";

    public static readonly string[] All = [Warn, Suspend, Ban, Unban];
}
