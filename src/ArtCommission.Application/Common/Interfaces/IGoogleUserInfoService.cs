namespace ArtCommission.Application.Common.Interfaces;

public interface IGoogleUserInfoService
{
    /// <summary>
    /// Xác thực access_token với Google rồi lấy thông tin user đã xác thực.
    /// Trả về null cho mọi lỗi mong đợi (token sai/hết hạn/aud không khớp) —
    /// không throw, để caller xử lý như lỗi validate bình thường.
    /// </summary>
    Task<GoogleUserInfo?> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default);
}

public record GoogleUserInfo(string Sub, string Email, bool EmailVerified, string? Name);
