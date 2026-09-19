using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Payment.Common;

/// <summary>Một dòng voucher trả cho danh sách (UC50).</summary>
public record VoucherDto(
    Guid VoucherId,
    string VoucherCode,
    string? Name,
    string DiscountType,
    decimal DiscountValue,
    decimal MinOrderAmount,
    decimal? MaxDiscountAmount,
    string[] Scope,
    DateOnly StartDate,
    DateOnly EndDate,
    int? UsageLimit,
    int UsedCount,
    bool IsActive,
    /// <summary>Scheduled / Active / Expired / Disabled / UsedUp — suy ra, không lưu trong DB.</summary>
    string VoucherStatus,
    /// <summary>Null = voucher toàn sàn do Admin tạo.</summary>
    Guid? CreatedByUserId,
    /// <summary>True nếu voucher do chính người đang gọi tạo ra.</summary>
    bool IsMine,
    DateTimeOffset CreatedAt
);

/// <summary>Một lượt đã dùng voucher — trả ở màn chi tiết (UC50).</summary>
public record VoucherRedemptionDto(
    Guid VoucherRedemptionId,
    Guid UserId,
    string RefType,
    Guid RefId,
    decimal DiscountAmount,
    DateTimeOffset RedeemedAt
);

/// <summary>Kết quả kiểm tra mã ở bước checkout (UC50).</summary>
public record VoucherValidationDto(
    Guid? VoucherId,
    bool IsValid,
    decimal DiscountAmount,
    decimal FinalAmount,
    string? Reason
);

/// <summary>Kết quả ghi nhận sử dụng voucher (UC50).</summary>
public record VoucherRedeemedDto(
    Guid VoucherRedemptionId,
    Guid VoucherId,
    string VoucherCode,
    decimal DiscountAmount,
    decimal OrderAmount,
    decimal FinalAmount,
    DateTimeOffset RedeemedAt
);

public static class VoucherResponseMapper
{
    /// <summary>
    /// Trạng thái hiển thị của voucher — SUY RA từ dữ liệu, không đọc từ cột nào.
    /// Thứ tự ưu tiên: bị tắt → hết hạn → chưa tới hạn → hết lượt → đang chạy.
    /// </summary>
    public static string ResolveStatus(Voucher voucher, DateOnly today)
    {
        if (!voucher.IsActive)
        {
            return VoucherStatusNames.Disabled;
        }

        if (today > voucher.EndDate)
        {
            return VoucherStatusNames.Expired;
        }

        if (today < voucher.StartDate)
        {
            return VoucherStatusNames.Scheduled;
        }

        if (voucher.UsageLimit.HasValue && voucher.UsedCount >= voucher.UsageLimit.Value)
        {
            return VoucherStatusNames.UsedUp;
        }

        return VoucherStatusNames.Active;
    }

    public static VoucherDto ToDto(Voucher voucher, DateOnly today, Guid? viewerUserId = null) => new(
        voucher.Id,
        voucher.VoucherCode,
        voucher.Name,
        voucher.DiscountType.ToString(),
        voucher.DiscountValue,
        voucher.MinOrderAmount,
        voucher.MaxDiscountAmount,
        VoucherScopeParser.ToNames(voucher.Scope),
        voucher.StartDate,
        voucher.EndDate,
        voucher.UsageLimit,
        voucher.UsedCount,
        voucher.IsActive,
        ResolveStatus(voucher, today),
        voucher.CreatedByUserId,
        voucher.CreatedByUserId.HasValue && voucher.CreatedByUserId == viewerUserId,
        voucher.CreatedAt
    );

    public static VoucherRedemptionDto ToDto(VoucherRedemption redemption) => new(
        redemption.Id,
        redemption.UserId,
        redemption.RefType,
        redemption.RefId,
        redemption.DiscountAmount,
        redemption.RedeemedAt
    );
}
