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

    /// <summary>Mã Admin thực hiện cập nhật cấu hình lần cuối (theo database.sql schema).</summary>
    public Guid? UpdatedByAdminId { get; set; }
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

    /// <summary>Khoá tương đương trong docs/database.sql: tỷ lệ phí sàn (0.05..0.15).</summary>
    public const string PlatformFeeRateSqlKey = "platform_fee_rate";

    /// <summary>Phần trăm tiền cọc phải khoá khi đặt giá đấu giá.</summary>
    public const string AuctionHoldPercent = "AuctionHoldPercent";

    /// <summary>
    /// Số giờ người thắng phiên đấu giá phải hoàn tất thanh toán, tính từ lúc chốt phiên.
    ///
    /// Bắt buộc phải có giá trị để `Auction.PaymentDeadline` được ghi lúc chốt phiên.
    /// Không có hạn thanh toán thì endpoint xử lý quá hạn
    /// (`POST /auctions/{auctionId}/settlement/expire`) luôn từ chối vì không biết
    /// "quá hạn" là khi nào — endpoint trở thành chết.
    /// </summary>
    public const string AuctionPaymentWindowHours = "AuctionPaymentWindowHours";

    /// <summary>Bật/tắt chức năng rút tiền ("true"/"false").</summary>
    public const string PayoutEnabled = "PayoutEnabled";

    /// <summary>Số ngày tự động duyệt cột mốc nếu Client không phản hồi (mặc định 7 ngày - BR-37).</summary>
    public const string MilestoneAutoApprovalDays = "MilestoneAutoApprovalDays";

    /// <summary>Khoá tương đương trong docs/database.sql: số ngày giữ tiền escrow.</summary>
    public const string EscrowHoldDaysSqlKey = "escrow_hold_days";

    /// <summary>Số lượt yêu cầu sửa đổi miễn phí mặc định cho mỗi dịch vụ/cột mốc (mặc định 2).</summary>
    public const string DefaultFreeRevisionLimit = "DefaultFreeRevisionLimit";

    /// <summary>Khoá tương đương trong docs/database.sql: số lần sửa đổi tối đa mặc định.</summary>
    public const string MaxRevisionCountSqlKey = "max_revision_count";

    /// <summary>Thời gian hết hạn của presigned URL xem/tải file (phút, mặc định 15 phút - BR-39).</summary>
    public const string PresignedUrlExpirationMinutes = "PresignedUrlExpirationMinutes";
}
