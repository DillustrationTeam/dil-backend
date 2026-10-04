namespace ArtCommission.Infrastructure.ExternalServices.Google;

/// <summary>
/// Cấu hình Google OAuth. Bind từ section "GoogleAuth" trong configuration.
/// Chỉ cần Client ID (public) — flow này không dùng Client Secret vì backend
/// không tự đổi authorization code, chỉ xác thực access_token do client gửi lên.
/// </summary>
public class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";

    public string ClientId { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);
}
