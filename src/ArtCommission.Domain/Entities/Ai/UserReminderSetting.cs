using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Ai;

/// <summary>
/// Cấu hình kênh nhận nhắc nhở deadline của một người dùng (UC46).
/// Quan hệ 1-1 với <c>ApplicationUser</c> — mỗi user tối đa một dòng.
///
/// <see cref="LeadHours"/> là số giờ nhắc TRƯỚC hạn: hạn 20:00 ngày mai với
/// LeadHours = 24 thì nhắc từ 20:00 hôm nay.
/// </summary>
public class UserReminderSetting : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>Nhận nhắc qua email.</summary>
    public bool EmailEnabled { get; set; } = true;

    /// <summary>Nhận nhắc qua push/in-app real-time.</summary>
    public bool PushEnabled { get; set; } = true;

    /// <summary>Số giờ nhắc trước hạn. Mặc định 24 giờ.</summary>
    public int LeadHours { get; set; } = 24;
}
