namespace ArtCommission.Application.Common.Interfaces;

/// <summary>
/// "Vé" ngắn hạn (stateless, ký HMAC) chứng minh một email đã được xác minh qua mã OTP
/// trước khi tài khoản tồn tại — RegisterCommand dùng vé này thay vì tra lại DB.
/// </summary>
public interface IEmailVerificationService
{
    string GenerateTicket(string email);

    bool ValidateTicket(string email, string ticket);
}
