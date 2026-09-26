namespace ArtCommission.Domain.Enums;

/// <summary>
/// Cách tính tiền giảm của voucher (UC50).
/// </summary>
public enum DiscountType
{
    /// <summary>Giảm theo phần trăm — <c>DiscountValue</c> là 0..100, chặn trần bởi <c>MaxDiscountAmount</c>.</summary>
    Percent,

    /// <summary>Giảm số tiền cố định (VND) — không vượt quá giá trị đơn hàng.</summary>
    Fixed
}

public static class DiscountTypeNames
{
    public const string Percent = nameof(DiscountType.Percent);
    public const string Fixed = nameof(DiscountType.Fixed);

    public static readonly string[] All = [Percent, Fixed];
}

/// <summary>
/// Voucher áp dụng được cho loại giao dịch nào (UC50).
///
/// Là CỜ BIT (bitmask) nên 1 voucher có thể dùng cho nhiều loại giao dịch cùng lúc.
/// Trong DB lưu dạng int; trong API nhận/trả mảng chuỗi: ["Commission", "Deposit"].
/// </summary>
[Flags]
public enum VoucherScope
{
    None = 0,

    /// <summary>Đơn đặt vẽ (escrow Commission).</summary>
    Commission = 1,

    /// <summary>Thắng / mua ngay ở phiên đấu giá.</summary>
    Auction = 2,

    /// <summary>Nạp tiền vào ví (UC48).</summary>
    Deposit = 4,

    All = Commission | Auction | Deposit
}

public static class VoucherScopeNames
{
    public const string Commission = nameof(VoucherScope.Commission);
    public const string Auction = nameof(VoucherScope.Auction);
    public const string Deposit = nameof(VoucherScope.Deposit);

    public static readonly string[] All = [Commission, Auction, Deposit];
}

/// <summary>
/// Trạng thái voucher — suy ra từ <c>IsActive</c> + hạn dùng, KHÔNG lưu cột riêng
/// (lưu riêng sẽ lệch trạng thái khi hết hạn mà không ai cập nhật).
/// </summary>
public enum VoucherStatus
{
    /// <summary>Chưa tới <c>StartDate</c>.</summary>
    Scheduled,

    /// <summary>Đang trong hạn dùng và <c>IsActive = true</c>.</summary>
    Active,

    /// <summary>Đã qua <c>EndDate</c>.</summary>
    Expired,

    /// <summary>Bị quản trị viên tắt (<c>IsActive = false</c>).</summary>
    Disabled,

    /// <summary>Đã dùng hết <c>UsageLimit</c>.</summary>
    UsedUp
}

public static class VoucherStatusNames
{
    public const string Scheduled = nameof(VoucherStatus.Scheduled);
    public const string Active = nameof(VoucherStatus.Active);
    public const string Expired = nameof(VoucherStatus.Expired);
    public const string Disabled = nameof(VoucherStatus.Disabled);
    public const string UsedUp = nameof(VoucherStatus.UsedUp);

    public static readonly string[] All = [Scheduled, Active, Expired, Disabled, UsedUp];
}
