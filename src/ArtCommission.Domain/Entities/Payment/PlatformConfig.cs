using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Payment;

/// <summary>
/// Cấu hình phí &amp; chính sách của sàn, lưu dạng key-value để Admin sửa không cần deploy.
/// Ví dụ key: MinPayoutAmount, PlatformFeePercent, AuctionHoldPercent, PayoutEnabled.
/// </summary>
public class PlatformConfig : BaseEntity
{
    /// <summary>Khoá cấu hình — unique.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Giá trị lưu dạng chuỗi, parse theo ngữ cảnh sử dụng.</summary>
    public string Value { get; set; } = string.Empty;

    public string? Description { get; set; }
}

/// <summary>
/// Tên các khoá cấu hình đã dùng trong hệ thống — tránh gõ sai chuỗi rải rác trong code.
/// </summary>
public static class PlatformConfigKeys
{
    /// <summary>Số tiền rút tối thiểu (VND) — dùng ở POST /payout-requests.</summary>
    public const string MinPayoutAmount = "MinPayoutAmount";

    /// <summary>Phần trăm phí nền tảng trên mỗi giao dịch.</summary>
    public const string PlatformFeePercent = "PlatformFeePercent";

    /// <summary>Phần trăm tiền cọc phải khoá khi đặt giá đấu giá.</summary>
    public const string AuctionHoldPercent = "AuctionHoldPercent";

    /// <summary>Bật/tắt chức năng rút tiền ("true"/"false").</summary>
    public const string PayoutEnabled = "PayoutEnabled";
}
