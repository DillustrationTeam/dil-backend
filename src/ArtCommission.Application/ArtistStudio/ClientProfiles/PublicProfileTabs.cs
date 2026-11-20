namespace ArtCommission.Application.ArtistStudio.ClientProfiles;

/// <summary>
/// Các tab trên trang hồ sơ công khai của Client mà chủ tài khoản có thể bật/tắt hiển thị cho người khác.
/// "requested" (đơn đã đặt) không nằm trong danh sách này — luôn riêng tư, không thể bật công khai.
/// </summary>
public static class PublicProfileTabs
{
    public const string Reputation = "reputation";
    public const string Saved = "saved";
    public const string Liked = "liked";

    public static readonly string[] All = [Reputation, Saved, Liked];
}
