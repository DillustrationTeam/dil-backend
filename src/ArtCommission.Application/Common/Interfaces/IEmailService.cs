namespace ArtCommission.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendEmailVerificationAsync(string toEmail, string fullName, string verificationToken, CancellationToken cancellationToken = default);

    /// <summary>Gửi mã OTP 6 số để xác minh email trước khi tài khoản được tạo (đăng ký).</summary>
    Task SendVerificationCodeEmailAsync(string toEmail, string code, CancellationToken cancellationToken = default);

    /// <summary>Gửi mã OTP 6 số để xác minh danh tính khi người dùng quên mật khẩu.</summary>
    Task SendPasswordResetCodeEmailAsync(string toEmail, string code, CancellationToken cancellationToken = default);

    /// <summary>Gửi mã OTP 6 số tới email MỚI khi người dùng đang đổi email.</summary>
    Task SendChangeEmailCodeEmailAsync(string toEmail, string code, CancellationToken cancellationToken = default);

    /// <summary>Gửi thông báo tới email CŨ sau khi đổi email thành công (cảnh báo bảo mật).</summary>
    Task SendEmailChangedNoticeAsync(string oldEmail, string newEmail, CancellationToken cancellationToken = default);

    /// <summary>Gửi mã OTP 6 số tới email HIỆN TẠI để xác thực lại danh tính (tài khoản không có mật khẩu) trước khi đổi email.</summary>
    Task SendReauthCodeEmailAsync(string toEmail, string code, CancellationToken cancellationToken = default);
}
