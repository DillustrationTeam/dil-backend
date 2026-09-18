using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Payment;

/// <summary>
/// Mã giảm giá dùng cho giao dịch (UC50).
///
/// Vòng đời: tạo (Admin/Creator) → người dùng nhập ở checkout
/// (<c>POST /vouchers/validate</c>, chỉ ĐỌC) → giao dịch được xác nhận
/// (<c>POST /vouchers/redeem</c> mới thực sự tiêu lượt).
///
/// <see cref="UsedCount"/> là số đếm denormalize để chặn vượt <see cref="UsageLimit"/>
/// bằng một câu UPDATE có điều kiện (atomic), không phải đọc-rồi-ghi.
/// Sổ chi tiết ai đã dùng nằm ở <see cref="VoucherRedemption"/>.
/// </summary>
public class Voucher : BaseEntity
{
    /// <summary>Mã người dùng nhập — lưu CHỮ HOA, duy nhất toàn hệ thống.</summary>
    public string VoucherCode { get; set; } = string.Empty;

    /// <summary>
    /// Người tạo voucher. Admin tạo thì NULL (voucher toàn sàn);
    /// Creator tạo thì lưu id Creator — dùng để lọc "voucher của tôi".
    /// </summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>Tên hiển thị cho người dùng (không bắt buộc).</summary>
    public string? Name { get; set; }

    public DiscountType DiscountType { get; set; }

    /// <summary>
    /// <see cref="DiscountType.Percent"/> → phần trăm 0..100.
    /// <see cref="DiscountType.Fixed"/> → số tiền VND.
    /// </summary>
    public decimal DiscountValue { get; set; }

    /// <summary>Giá trị đơn tối thiểu mới được áp dụng (VND). 0 = không yêu cầu.</summary>
    public decimal MinOrderAmount { get; set; }

    /// <summary>Trần tiền giảm cho voucher Percent (VND). Null = không chặn trần.</summary>
    public decimal? MaxDiscountAmount { get; set; }

    /// <summary>Áp dụng được cho những loại giao dịch nào.</summary>
    public VoucherScope Scope { get; set; } = VoucherScope.All;

    /// <summary>Ngày bắt đầu hiệu lực (theo giờ Việt Nam, so sánh ở mức NGÀY).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Ngày hết hiệu lực — được dùng tới HẾT ngày này (bao gồm cả ngày kết thúc).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Tổng số lượt được phép dùng. Null = không giới hạn.</summary>
    public int? UsageLimit { get; set; }

    /// <summary>Số lượt đã tiêu thụ thật (chỉ tăng ở <c>POST /vouchers/redeem</c>).</summary>
    public int UsedCount { get; set; }

    /// <summary>Admin/Creator tắt thủ công. Khác với hết hạn.</summary>
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<VoucherRedemption> Redemptions { get; set; } = [];
}
