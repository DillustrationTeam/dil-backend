using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Payment;

/// <summary>
/// Cấu hình phí &amp; chính sách của sàn, lưu dạng key-value để Admin sửa không cần deploy.
/// Ví dụ key: MinPayoutAmount, PlatformFeePercent, AuctionHoldPercent, PayoutEnabled.
/// </summary>
public class PlatformConfig : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? UpdatedByAdminId { get; set; }
}

/// <summary>
/// Tên các khoá cấu hình đã dùng trong hệ thống — tránh gõ sai chuỗi rải rác trong code.
/// </summary>
public static class PlatformConfigKeys
{
    public const string MinPayoutAmount = "MinPayoutAmount";
    public const string PlatformFeePercent = "PlatformFeePercent";
    public const string PlatformFeeRateSqlKey = "platform_fee_rate";
    public const string AuctionHoldPercent = "AuctionHoldPercent";
    public const string PayoutEnabled = "PayoutEnabled";
    public const string MilestoneAutoApprovalDays = "MilestoneAutoApprovalDays";
    public const string EscrowHoldDaysSqlKey = "escrow_hold_days";

    public const string DefaultFreeRevisionLimit = "DefaultFreeRevisionLimit";

    public const string MaxRevisionCountSqlKey = "max_revision_count";

    public const string PresignedUrlExpirationMinutes = "PresignedUrlExpirationMinutes";
}
