using ArtCommission.Application.Common.DTOs;

namespace ArtCommission.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<(bool Success, Guid UserId, string[] Errors)> RegisterUserAsync(string email, string password, string fullName, string? role = null, bool isVerified = false, CancellationToken cancellationToken = default);
    Task<(bool Success, UserDto? User, string[] Roles, string[] Errors)> AuthenticateUserAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<(bool Success, UserDto? User, string[] Roles, string[] Errors)> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsEmailUniqueAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> VerifyUserEmailAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<(bool Success, string[] Errors)> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    Task<string> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken = default);
    Task<(bool Success, string[] Errors)> ResetPasswordAsync(string email, string resetToken, string newPassword, CancellationToken cancellationToken = default);
    Task<(bool Success, string[] Errors)> AddCreatorRoleAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đăng nhập hoặc tự động tạo tài khoản qua external login (vd Google OAuth).
    /// Ưu tiên tìm theo (provider, providerKey) đã link trước đó; nếu chưa có,
    /// tìm theo email — nếu email đã xác thực (emailVerified) thì merge/link vào
    /// tài khoản có sẵn; nếu chưa từng có tài khoản nào thì tạo mới (không mật khẩu).
    /// </summary>
    Task<(bool Success, UserDto? User, string[] Roles, string[] Errors)> AuthenticateOrRegisterExternalAsync(
        string provider, string providerKey, string email, bool emailVerified, string? fullName,
        CancellationToken cancellationToken = default);
}
