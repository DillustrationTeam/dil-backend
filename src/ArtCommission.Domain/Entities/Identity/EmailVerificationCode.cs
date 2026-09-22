using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Identity;

/// <summary>
/// Mã xác minh 6 số dùng chung cho 2 mục đích (xem <see cref="Purpose"/>): xác minh email
/// trước khi tài khoản được tạo (đăng ký), hoặc xác minh danh tính khi quên mật khẩu.
/// Không gắn với ApplicationUser vì ở luồng đăng ký, tài khoản chưa tồn tại tại thời điểm gửi mã.
/// </summary>
public class EmailVerificationCode : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public VerificationCodePurpose Purpose { get; set; } = VerificationCodePurpose.EmailVerification;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public int AttemptCount { get; set; }
}
