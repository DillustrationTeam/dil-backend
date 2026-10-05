using ArtCommission.Application.Common.DTOs;

namespace ArtCommission.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<(bool Success, Guid UserId, string[] Errors)> RegisterUserAsync(string email, string password, string fullName, string? role = null, bool isVerified = false, CancellationToken cancellationToken = default);
    /// <summary>Đăng nhập bằng email HOẶC username (ClientProfile.Username) — thử tìm theo email trước, không thấy thì thử theo username.</summary>
    Task<(bool Success, UserDto? User, string[] Roles, string[] Errors)> AuthenticateUserAsync(string emailOrUsername, string password, CancellationToken cancellationToken = default);
    Task<(bool Success, UserDto? User, string[] Roles, string[] Errors)> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<(bool Success, UserDto? User, string[] Roles, string[] Errors)> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> IsEmailUniqueAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> VerifyUserEmailAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<(bool Success, string[] Errors)> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    Task<bool> HasPasswordAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<(bool Success, string[] Errors)> AddPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default);
    Task<string> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken = default);
    Task<(bool Success, string[] Errors)> ResetPasswordAsync(string email, string resetToken, string newPassword, CancellationToken cancellationToken = default);
    Task<(bool Success, string[] Errors)> AddCreatorRoleAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Đổi email đăng nhập của user đã xác thực OTP tới email mới (cập nhật cả UserName theo email mới).</summary>
    Task<(bool Success, string[] Errors)> ChangeEmailAsync(Guid userId, string newEmail, CancellationToken cancellationToken = default);

    /// <summary>Danh sách external login provider (vd "Google") đã liên kết, kèm user có mật khẩu hay chưa.</summary>
    Task<(bool HasPassword, IReadOnlyList<string> LinkedProviders)> GetLinkedAccountsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Gỡ liên kết 1 external login provider — chặn nếu đây là cách đăng nhập duy nhất còn lại (tránh khóa tài khoản).</summary>
    Task<(bool Success, string[] Errors)> UnlinkExternalLoginAsync(Guid userId, string provider, CancellationToken cancellationToken = default);

    /// <summary>Liên kết thêm 1 external login provider vào tài khoản đang đăng nhập — chặn nếu provider đó đã gắn với tài khoản KHÁC.</summary>
    Task<(bool Success, string[] Errors)> LinkExternalLoginAsync(Guid userId, string provider, string providerKey, CancellationToken cancellationToken = default);

    Task<bool> VerifyPasswordAsync(Guid userId, string password, CancellationToken cancellationToken = default);

    /// <summary>Vô hiệu hóa tài khoản (soft-delete): set IsDeleted + DeletedAt. Không kiểm tra nghiệp vụ — gọi sau khi đã validate ở command.</summary>
    Task<(bool Success, string[] Errors)> DeactivateAccountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đăng nhập hoặc link tài khoản qua external login (vd Google OAuth).
    /// Ưu tiên tìm theo (provider, providerKey) đã link trước đó; nếu chưa có,
    /// tìm theo email — nếu email đã xác thực (emailVerified) thì merge/link vào
    /// tài khoản có sẵn. Nếu chưa từng có tài khoản nào, KHÔNG tự tạo nữa —
    /// trả về PendingRegistration để FE hoàn tất form username + password trước
    /// (xem <see cref="RegisterExternalUserAsync"/>).
    /// </summary>
    Task<(bool Success, UserDto? User, string[] Roles, ExternalRegistrationPendingDto? PendingRegistration, string[] Errors)> AuthenticateOrRegisterExternalAsync(
        string provider, string providerKey, string email, bool emailVerified, string? fullName,
        CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra username (chosen login handle) đã được dùng chưa (so khớp NormalizedUsername).</summary>
    Task<bool> IsUsernameUniqueAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hoàn tất đăng ký tài khoản mới qua external login sau khi FE đã thu thập username + password —
    /// đây là bước DUY NHẤT thực sự tạo user cho luồng Google đăng ký mới (không còn tạo ngầm không mật khẩu).
    /// </summary>
    Task<(bool Success, UserDto? User, string[] Roles, string[] Errors)> RegisterExternalUserAsync(
        string provider, string providerKey, string email, bool emailVerified, string? fullName,
        string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Thêm username + password lần đầu cho một tài khoản ĐÃ TỒN TẠI nhưng chưa có mật khẩu
    /// (vd tài khoản Google tạo trước khi tính năng bắt buộc username+password ra đời).
    /// Thất bại nếu tài khoản đã có password rồi (gọi AddPasswordAsync/ChangePasswordAsync thay) hoặc username đã bị chiếm.
    /// </summary>
    Task<(bool Success, string[] Errors)> CompleteAccountSetupAsync(Guid userId, string username, string password, CancellationToken cancellationToken = default);

    /// <summary>Sinh (hoặc tái sử dụng nếu đã có nhưng chưa bật) authenticator key TOTP, trả về secret key + otpauth:// URI để FE vẽ QR.</summary>
    Task<(bool Success, string SharedKey, string AuthenticatorUri, string[] Errors)> Setup2FAAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Xác minh mã TOTP đầu tiên rồi mới chính thức bật 2FA, sinh 10 recovery code mới (chỉ hiện đúng 1 lần ngay lúc này).</summary>
    Task<(bool Success, string[] RecoveryCodes, string[] Errors)> VerifyAndEnable2FAAsync(Guid userId, string code, CancellationToken cancellationToken = default);

    /// <summary>Tắt 2FA và reset authenticator key — gọi sau khi command đã xác thực lại mật khẩu.</summary>
    Task<(bool Success, string[] Errors)> Disable2FAAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> IsTwoFactorEnabledAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xác minh mã 2FA lúc login — thử mã TOTP trước, nếu sai thử như recovery code (tiêu luôn nếu đúng).
    /// CodeAlreadyUsed=true khi mã đúng định dạng và từng được cấp cho user này nhưng đã bị tiêu trước đó.
    /// </summary>
    Task<(bool Success, bool IsRecoveryCode, bool CodeAlreadyUsed)> VerifyTwoFactorCodeAsync(Guid userId, string code, CancellationToken cancellationToken = default);
}
