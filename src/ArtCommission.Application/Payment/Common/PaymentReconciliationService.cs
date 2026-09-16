using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Payment.Common;

/// <summary>Kết quả một lượt đối soát.</summary>
public record ReconciliationSummary(int Scanned, int Credited, int Expired, int Failed, int Errors);

public interface IPaymentReconciliationService
{
    /// <summary>
    /// Quét các đơn nạp còn Pending quá lâu, đối chiếu với cổng:
    ///   - cổng báo PAID và đủ tiền  => cộng ví
    ///   - cổng báo CANCELLED/EXPIRED/FAILED => đóng đơn
    /// Job này bù cho trường hợp webhook không tới được (dev local, cổng retry hết lượt).
    /// </summary>
    Task<ReconciliationSummary> ReconcilePendingOrdersAsync(
        TimeSpan olderThan,
        int batchSize,
        CancellationToken cancellationToken = default);
}

public class PaymentReconciliationService : IPaymentReconciliationService
{
    private readonly IApplicationDbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly IPaymentSettlementService _settlement;
    private readonly ILogger<PaymentReconciliationService> _logger;

    public PaymentReconciliationService(
        IApplicationDbContext db,
        IPaymentGateway gateway,
        IPaymentSettlementService settlement,
        ILogger<PaymentReconciliationService> logger)
    {
        _db = db;
        _gateway = gateway;
        _settlement = settlement;
        _logger = logger;
    }

    public async Task<ReconciliationSummary> ReconcilePendingOrdersAsync(
        TimeSpan olderThan,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - olderThan;

        var candidates = await _db.PaymentOrders
            .AsNoTracking()
            .Where(o => o.Status == PaymentOrderStatus.Pending
                        && o.Gateway == PaymentGateway.PayOS
                        && o.CreatedAt <= cutoff
                        && !o.IsDeleted)
            .OrderBy(o => o.CreatedAt)
            .Take(batchSize)
            .Select(o => new { o.Id, o.PayOsOrderCode, o.OrderRef })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return new ReconciliationSummary(0, 0, 0, 0, 0);
        }

        var credited = 0;
        var expired = 0;
        var failed = 0;
        var errors = 0;

        foreach (var candidate in candidates)
        {
            try
            {
                var link = await _gateway.GetPaymentLinkAsync(candidate.PayOsOrderCode, cancellationToken);

                if (link.Status == "PAID" && link.AmountPaid >= link.Amount)
                {
                    var transactionRef = link.Transactions?.FirstOrDefault()?.Reference;

                    var outcome = await _settlement.ApplyPaidOrderAsync(
                        candidate.Id,
                        transactionRef,
                        link.AmountPaid,
                        DateTimeOffset.UtcNow,
                        source: "job",
                        cancellationToken);

                    if (outcome.Applied)
                    {
                        credited++;
                        _logger.LogInformation(
                            "Job đối soát: đã cộng tiền cho đơn {OrderRef} (bù webhook không tới)",
                            candidate.OrderRef);
                    }
                }
                else if (link.Status is "CANCELLED" or "EXPIRED" or "FAILED" or "UNDERPAID")
                {
                    var order = await _db.PaymentOrders
                        .FirstOrDefaultAsync(o => o.Id == candidate.Id, cancellationToken);

                    if (order is not null && order.Status == PaymentOrderStatus.Pending)
                    {
                        order.Status = link.Status switch
                        {
                            "CANCELLED" => PaymentOrderStatus.Cancelled,
                            "EXPIRED" => PaymentOrderStatus.Expired,
                            // UNDERPAID: khách chuyển thiếu => coi như thất bại, không cộng tiền
                            _ => PaymentOrderStatus.Failed
                        };
                        order.FailureReason = $"Job đối soát: cổng báo {link.Status}.";
                        order.UpdatedAt = DateTimeOffset.UtcNow;
                        await _db.SaveChangesAsync(cancellationToken);

                        if (order.Status == PaymentOrderStatus.Expired)
                        {
                            expired++;
                        }
                        else
                        {
                            failed++;
                        }
                    }
                }
                // Còn PENDING / PROCESSING => để lượt sau
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogWarning(
                    ex, "Job đối soát lỗi khi xử lý đơn {OrderRef}", candidate.OrderRef);
            }
        }

        return new ReconciliationSummary(candidates.Count, credited, expired, failed, errors);
    }
}
