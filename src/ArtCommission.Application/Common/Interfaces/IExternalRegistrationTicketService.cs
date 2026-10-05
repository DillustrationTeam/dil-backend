namespace ArtCommission.Application.Common.Interfaces;

/// <summary>
/// Sinh/xác minh vé (ticket) stateless mang theo thông tin external login (provider, providerKey, email, fullName)
/// trong khoảng thời gian ngắn giữa bước "Google xác thực xong nhưng chưa có tài khoản" và bước
/// "user điền username + password để hoàn tất đăng ký" — tránh phải lưu trạng thái tạm vào DB.
/// </summary>
public interface IExternalRegistrationTicketService
{
    string GenerateTicket(string provider, string providerKey, string email, bool emailVerified, string? fullName);

    bool TryValidateTicket(
        string ticket,
        out string provider,
        out string providerKey,
        out string email,
        out bool emailVerified,
        out string? fullName);
}
