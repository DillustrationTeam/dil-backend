using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Payment.Common;

/// <summary>Kết quả sau khi xử lý một khoản thanh toán.</summary>
/// <param name="Applied">True nếu lần gọi này thực sự cộng tiền. False nếu bị bỏ qua (trùng/lệch).</param>
/// <param name="WalletTransactionId">Id dòng sổ cái vừa ghi.</param>
/// <param name="Reason">Lý do bỏ qua — để ghi log, KHÔNG trả ra client.</param>
public record PaymentSettlementOutcome(bool Applied, Guid? WalletTransactionId, string? Reason);

public interface IPaymentSettlementService
{
    /// <summary>
    /// Cộng tiền vào ví cho một đơn nạp đã được cổng xác nhận.
    /// Có tính IDEMPOTENT: gọi lại nhiều lần cũng chỉ cộng tiền đúng 1 lần.
    /// </summary>
    Task<PaymentSettlementOutcome> ApplyPaidOrderAsync(
        Guid paymentOrderId,
        string? transactionRef,
        long? paidAmount,
        DateTimeOffset? paidAt,
        string source,
        CancellationToken cancellationToken = default);
}

public class PaymentSettlementService : IPaymentSettlementService
{
    private readonly IApplicationDbContext _db;
    private readonly IWalletService _walletService;

    public PaymentSettlementService(IApplicationDbContext db, IWalletService walletService)
    {
        _db = db;
        _walletService = walletService;
    }

    public async Task<PaymentSettlementOutcome> ApplyPaidOrderAsync(
        Guid paymentOrderId,
        string? transactionRef,
        long? paidAmount,
        DateTimeOffset? paidAt,
        string source,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Đọc lại trong transaction để tránh 2 request xử lý song song cùng cộng tiền
        var order = await _db.PaymentOrders
            .FirstOrDefaultAsync(o => o.Id == paymentOrderId && !o.IsDeleted, cancellationToken);

        if (order is null)
        {
            return new PaymentSettlementOutcome(false, null, "Không tìm thấy đơn nạp tiền.");
        }

        // (1) Idempotency theo trạng thái đơn
        if (order.Status == PaymentOrderStatus.Paid)
        {
            return new PaymentSettlementOutcome(false, null, "Đơn đã ở trạng thái Paid — bỏ qua.");
        }

        if (order.Status is PaymentOrderStatus.Cancelled)
        {
            return new PaymentSettlementOutcome(false, null, "Đơn đã bị huỷ — bỏ qua.");
        }

        // (2) Idempotency theo mã giao dịch của cổng
        if (!string.IsNullOrEmpty(transactionRef))
        {
            var duplicated = await _db.PaymentOrders.AnyAsync(
                o => o.Id != order.Id
                     && o.Gateway == order.Gateway
                     && o.TransactionRef == transactionRef
                     && !o.IsDeleted,
                cancellationToken);

            if (duplicated)
            {
                return new PaymentSettlementOutcome(
                    false, null, $"Mã giao dịch {transactionRef} đã dùng cho đơn khác — bỏ qua.");
            }
        }

        // (3) Số tiền cổng báo phải khớp số tiền đặt
        if (paidAmount.HasValue && paidAmount.Value != (long)order.Amount)
        {
            order.Status = PaymentOrderStatus.Failed;
            order.FailureReason =
                $"Số tiền cổng báo ({paidAmount.Value}) lệch số tiền đặt ({(long)order.Amount}). Nguồn: {source}.";
            order.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new PaymentSettlementOutcome(false, null, order.FailureReason);
        }

        var wallet = await _walletService.GetOrCreateWalletAsync(order.UserId, cancellationToken);

        // (4) Cộng số dư
        wallet.Balance += order.Amount;
        wallet.UpdatedAt = DateTimeOffset.UtcNow;

        // (5) Đánh dấu đơn đã trả
        order.Status = PaymentOrderStatus.Paid;
        order.TransactionRef = transactionRef ?? order.TransactionRef;
        order.PaidAt = paidAt ?? DateTimeOffset.UtcNow;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        // (6) Ghi sổ cái + lưu tất cả trong CÙNG transaction
        var ledgerEntry = await _walletService.RecordTransactionAsync(
            wallet,
            WalletTransactionType.Deposit,
            WalletTransactionDirection.In,
            order.Amount,
            nameof(Domain.Entities.Payment.PaymentOrder),
            order.Id,
            $"Nạp tiền qua {order.Gateway} — đơn {order.OrderRef}",
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new PaymentSettlementOutcome(true, ledgerEntry.Id, null);
    }
}
