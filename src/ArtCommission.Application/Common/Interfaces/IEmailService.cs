namespace ArtCommission.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendEmailVerificationAsync(string toEmail, string fullName, string verificationToken, CancellationToken cancellationToken = default);

    /// <summary>Gửi mã OTP 6 số để xác minh email trước khi tài khoản được tạo (đăng ký).</summary>
    Task SendVerificationCodeEmailAsync(string toEmail, string code, CancellationToken cancellationToken = default);

    /// <summary>Gửi mã OTP 6 số để xác minh danh tính khi người dùng quên mật khẩu.</summary>
    Task SendPasswordResetCodeEmailAsync(string toEmail, string code, CancellationToken cancellationToken = default);
}
