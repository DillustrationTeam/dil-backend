using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.Payment;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Payment.Commands.RedeemVoucher;

/// <summary>
/// UC50 — POST /api/v1/vouchers/redeem
/// Ghi nhận ĐÃ DÙNG voucher khi giao dịch được xác nhận.
///
/// Khác <c>/vouchers/validate</c> (chỉ đọc): handler này TIÊU 1 lượt thật.
/// Vì vậy phải chống 3 kiểu sai:
///   1. Gọi 2 lần cho cùng 1 giao dịch  → unique index (RefType, RefId).
///   2. Nhiều request song song vượt UsageLimit → UPDATE có điều kiện (atomic).
///   3. Ghi nửa chừng rồi lỗi → tất cả nằm trong 1 transaction ACID.
/// </summary>
public record RedeemVoucherCommand(
    Guid UserId,
    string VoucherCode,
    string RefType,
    Guid RefId,
    decimal OrderAmount
) : IRequest<(bool Success, bool Conflict, VoucherRedeemedDto? Data, string[] Errors)>;

public class RedeemVoucherCommandHandler
    : IRequestHandler<RedeemVoucherCommand, (bool, bool, VoucherRedeemedDto?, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly IVoucherCheckService _voucherCheck;
    private readonly ILogger<RedeemVoucherCommandHandler> _logger;

    public RedeemVoucherCommandHandler(
        IApplicationDbContext db,
        IVoucherCheckService voucherCheck,
        ILogger<RedeemVoucherCommandHandler> logger)
    {
        _db = db;
        _voucherCheck = voucherCheck;
        _logger = logger;
    }

    public async Task<(bool, bool, VoucherRedeemedDto?, string[])> Handle(
        RedeemVoucherCommand request,
        CancellationToken cancellationToken)
    {
        if (request.RefId == Guid.Empty)
        {
            return (false, false, null, ["Thiếu id giao dịch cần áp voucher."]);
        }

        // Cửa sớm: đã có lượt dùng cho giao dịch này ⇒ trả luôn bản ghi cũ (idempotent),
        // KHÔNG tiêu thêm lượt và KHÔNG trả lỗi — gọi lại không được phá gì.
        var existing = await _db.VoucherRedemptions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.RefType == request.RefType && r.RefId == request.RefId,
                cancellationToken);

        if (existing is not null)
        {
            var existingCode = await _db.Vouchers
                .AsNoTracking()
                .Where(v => v.Id == existing.VoucherId)
                .Select(v => v.VoucherCode)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            _logger.LogInformation(
                "Voucher redeem gọi lại cho giao dịch đã áp mã. RefType={RefType} RefId={RefId} RedemptionId={RedemptionId}",
                request.RefType, request.RefId, existing.Id);

            return (true, false, new VoucherRedeemedDto(
                existing.Id,
                existing.VoucherId,
                existingCode,
                existing.DiscountAmount,
                existing.OrderAmount,
                existing.FinalAmount,
                existing.RedeemedAt), []);
        }

        var check = await _voucherCheck.CheckAsync(
            request.UserId,
            request.VoucherCode,
            request.OrderAmount,
            request.RefType,
            request.RefId,
            cancellationToken);

        if (!check.Success || check.Voucher is null)
        {
            return (false, false, null, check.Errors.Length > 0 ? check.Errors : [check.Reason ?? "Mã giảm giá không hợp lệ."]);
        }

        var voucher = check.Voucher;
        var now = DateTimeOffset.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Atomic: chỉ tăng khi CÒN lượt. 0 dòng = vừa bị người khác tiêu hết.
            var affected = await _db.Vouchers
                .Where(v => v.Id == voucher.Id
                            && !v.IsDeleted
                            && v.IsActive
                            && (v.UsageLimit == null || v.UsedCount < v.UsageLimit.Value))
                .ExecuteUpdateAsync(
                    s => s.SetProperty(v => v.UsedCount, v => v.UsedCount + 1)
                          .SetProperty(v => v.UpdatedAt, now),
                    cancellationToken);

            if (affected == 0)
            {
                await tx.RollbackAsync(cancellationToken);
                return (false, true, null, ["Mã giảm giá đã hết lượt sử dụng."]);
            }

            var redemption = new VoucherRedemption
            {
                VoucherId = voucher.Id,
                UserId = request.UserId,
                RefType = request.RefType,
                RefId = request.RefId,
                DiscountAmount = check.DiscountAmount,
                OrderAmount = request.OrderAmount,
                FinalAmount = check.FinalAmount,
                RedeemedAt = now
            };

            _db.VoucherRedemptions.Add(redemption);
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Đã tiêu voucher. VoucherCode={VoucherCode} VoucherId={VoucherId} UserId={UserId} RefType={RefType} RefId={RefId} Discount={Discount}",
                voucher.VoucherCode, voucher.Id, request.UserId, request.RefType, request.RefId, check.DiscountAmount);

            return (true, false, new VoucherRedeemedDto(
                redemption.Id,
                voucher.Id,
                voucher.VoucherCode,
                redemption.DiscountAmount,
                redemption.OrderAmount,
                redemption.FinalAmount,
                redemption.RedeemedAt), []);
        }
        catch (DbUpdateException ex) when (LooksLikeUniqueViolation(ex))
        {
            // Đua nhau: request khác đã ghi redemption cho cùng giao dịch trước.
            await tx.RollbackAsync(cancellationToken);

            _logger.LogWarning(
                "Voucher redeem trùng cho cùng giao dịch. RefType={RefType} RefId={RefId}",
                request.RefType, request.RefId);

            return (false, true, null, ["Giao dịch này đã được áp mã giảm giá rồi."]);
        }
    }

    /// <summary>
    /// Nhận biết lỗi vi phạm unique index.
    ///
    /// KHÔNG tham chiếu <c>Microsoft.Data.SqlClient</c> ở đây: tầng Application không được
    /// biết mình đang chạy trên SQL Server (xem <c>docs/01-architecture.md</c> — chiều phụ thuộc
    /// chỉ đi vào trong). Vì vậy nhận diện qua thông điệp lỗi của provider,
    /// và vẫn kiểm tra lại bằng truy vấn trước khi kết luận.
    /// </summary>
    private static bool LooksLikeUniqueViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;

        return message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
               || message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Cannot insert duplicate", StringComparison.OrdinalIgnoreCase);
    }
}
