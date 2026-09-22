using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Identity;

/// <summary>
/// Mã xác minh email 6 số, gửi trước khi tài khoản được tạo (đăng ký bằng email/password).
/// Không gắn với ApplicationUser vì tại thời điểm gửi mã, tài khoản chưa tồn tại.
/// </summary>
public class EmailVerificationCode : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public int AttemptCount { get; set; }
}
