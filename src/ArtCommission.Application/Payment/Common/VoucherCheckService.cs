using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Payment.Common;

/// <summary>Kết quả kiểm tra một mã voucher cho một đơn hàng cụ thể.</summary>
/// <param name="Success">False = không tra được/không dùng được, đọc <paramref name="Errors"/>.</param>
public record VoucherCheckResult(
    bool Success,
    Voucher? Voucher,
    decimal DiscountAmount,
    decimal FinalAmount,
    string? Reason,
    string[] Errors);

/// <summary>
/// Cổng kiểm tra voucher dùng chung cho MỌI nghiệp vụ tiêu voucher
/// (checkout Commission, thắng đấu giá, nạp tiền) — không viết lại logic ở từng nơi.
/// </summary>
public interface IVoucherCheckService
{
    /// <summary>
    /// Kiểm tra voucher có áp được cho đơn <paramref name="orderAmount"/> thuộc loại
    /// <paramref name="refType"/> hay không. CHỈ ĐỌC — không tiêu lượt.
    /// </summary>
    /// <param name="userId">Người đang áp mã (để chặn dùng lại cùng một giao dịch).</param>
    /// <param name="refId">Id giao dịch đang áp mã. Null = chỉ kiểm tra, chưa gắn giao dịch.</param>
    Task<VoucherCheckResult> CheckAsync(
        Guid userId,
        string voucherCode,
        decimal orderAmount,
        string refType,
        Guid? refId,
        CancellationToken cancellationToken = default);
}

public class VoucherCheckService : IVoucherCheckService
{
    /// <summary>
    /// Giờ Việt Nam (UTC+7) — hạn dùng voucher tính theo ngày của người dùng,
    /// không theo UTC, nếu không voucher sẽ hết hạn sớm 7 tiếng.
    /// </summary>
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private readonly IApplicationDbContext _db;

    public VoucherCheckService(IApplicationDbContext db)
    {
        _db = db;
    }

    public static DateOnly TodayInVietnam() =>
        DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(VietnamOffset).DateTime);

    /// <summary>
    /// Tính tiền giảm. Hàm THUẦN TUÝ — tách riêng để kiểm thử được mà không cần DB.
    /// Bất biến: 0 &lt;= kết quả &lt;= orderAmount (không bao giờ giảm quá giá trị đơn).
    /// </summary>
    public static decimal CalculateDiscount(
        DiscountType discountType,
        decimal discountValue,
        decimal? maxDiscountAmount,
        decimal orderAmount)
    {
        if (orderAmount <= 0 || discountValue <= 0)
        {
            return 0m;
        }

        decimal discount = discountType switch
        {
            // Làm tròn xuống để không bao giờ giảm lố cho nền tảng.
            DiscountType.Percent => Math.Floor(orderAmount * discountValue / 100m),
            DiscountType.Fixed => discountValue,
            _ => 0m
        };

        // Trần giảm giá chỉ áp cho voucher phần trăm.
        if (discountType == DiscountType.Percent
            && maxDiscountAmount.HasValue
            && maxDiscountAmount.Value > 0
            && discount > maxDiscountAmount.Value)
        {
            discount = maxDiscountAmount.Value;
        }

        return discount > orderAmount ? orderAmount : discount;
    }

    public async Task<VoucherCheckResult> CheckAsync(
        Guid userId,
        string voucherCode,
        decimal orderAmount,
        string refType,
        Guid? refId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(voucherCode))
        {
            return Fail("Vui lòng nhập mã giảm giá.");
        }

        if (orderAmount <= 0)
        {
            return Fail("Giá trị đơn hàng không hợp lệ.");
        }

        var scopeFlag = VoucherScopeParser.FromRefType(refType);
        if (scopeFlag == VoucherScope.None)
        {
            return Fail($"Loại giao dịch không hợp lệ. Hợp lệ: {string.Join(", ", VoucherScopeNames.All)}.");
        }

        var code = Normalize(voucherCode);

        var voucher = await _db.Vouchers
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.VoucherCode == code && !v.IsDeleted, cancellationToken);

        if (voucher is null)
        {
            return Fail("Mã giảm giá không tồn tại.");
        }

        var today = TodayInVietnam();

        if (!voucher.IsActive)
        {
            return Fail("Mã giảm giá đã bị vô hiệu hoá.", voucher);
        }

        if (today < voucher.StartDate)
        {
            return Fail($"Mã giảm giá chỉ bắt đầu có hiệu lực từ {voucher.StartDate:dd/MM/yyyy}.", voucher);
        }

        if (today > voucher.EndDate)
        {
            return Fail($"Mã giảm giá đã hết hạn từ {voucher.EndDate:dd/MM/yyyy}.", voucher);
        }

        if (voucher.UsageLimit.HasValue && voucher.UsedCount >= voucher.UsageLimit.Value)
        {
            return Fail("Mã giảm giá đã hết lượt sử dụng.", voucher);
        }

        if (!voucher.Scope.HasFlag(scopeFlag))
        {
            return Fail($"Mã giảm giá chỉ áp dụng cho: {VoucherScopeParser.Describe(voucher.Scope)}.", voucher);
        }

        if (orderAmount < voucher.MinOrderAmount)
        {
            return Fail(
                $"Đơn hàng tối thiểu {voucher.MinOrderAmount:N0}đ mới dùng được mã này.", voucher);
        }

        // Cùng một người không được dùng lại CÙNG một voucher cho CÙNG một giao dịch.
        if (refId.HasValue)
        {
            var alreadyRedeemed = await _db.VoucherRedemptions.AnyAsync(
                r => r.VoucherId == voucher.Id
                     && r.UserId == userId
                     && r.RefType == refType
                     && r.RefId == refId.Value,
                cancellationToken);

            if (alreadyRedeemed)
            {
                return Fail("Bạn đã dùng mã này cho giao dịch này rồi.", voucher);
            }
        }

        var discount = CalculateDiscount(
            voucher.DiscountType, voucher.DiscountValue, voucher.MaxDiscountAmount, orderAmount);

        if (discount <= 0)
        {
            return Fail("Mã giảm giá không giảm được đồng nào cho đơn hàng này.", voucher);
        }

        return new VoucherCheckResult(true, voucher, discount, orderAmount - discount, null, []);
    }

    /// <summary>Mã voucher lưu và so khớp ở dạng CHỮ HOA, cắt khoảng trắng.</summary>
    public static string Normalize(string voucherCode) => voucherCode.Trim().ToUpperInvariant();

    private static VoucherCheckResult Fail(string reason, Voucher? voucher = null) =>
        new(false, voucher, 0m, 0m, reason, [reason]);
}
