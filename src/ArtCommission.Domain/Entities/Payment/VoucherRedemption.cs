namespace ArtCommission.Domain.Entities.Payment;

/// <summary>
/// Ai đã dùng voucher nào cho giao dịch nào (UC50) — bảng ghi vết, chỉ THÊM.
///
/// KHÔNG kế thừa <c>BaseEntity</c> vì bản ghi này không bao giờ sửa/xoá mềm:
/// sửa hay xoá một dòng đã tiêu lượt sẽ làm <c>Voucher.UsedCount</c> không còn
/// đối soát được. Cần vô hiệu thì tắt voucher, không xoá vết.
/// </summary>
public class VoucherRedemption
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid VoucherId { get; set; }

    /// <summary>Người dùng đã áp mã.</summary>
    public Guid UserId { get; set; }

    /// <summary>Loại giao dịch được áp: "Commission", "Auction" hoặc "Deposit".</summary>
    public string RefType { get; set; } = string.Empty;

    /// <summary>Id giao dịch được áp (đơn đặt vẽ / phiên đấu giá / đơn nạp tiền).</summary>
    public Guid RefId { get; set; }

    /// <summary>Số tiền thực tế đã giảm (VND) — chốt tại thời điểm redeem, không tính lại.</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Giá trị đơn hàng lúc áp mã — phục vụ đối soát.</summary>
    public decimal OrderAmount { get; set; }

    /// <summary>Số tiền phải trả sau khi giảm.</summary>
    public decimal FinalAmount { get; set; }

    public DateTimeOffset RedeemedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public Voucher? Voucher { get; set; }
}
