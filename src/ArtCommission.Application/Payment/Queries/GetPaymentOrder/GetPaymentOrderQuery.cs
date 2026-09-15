using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Payment.Queries.GetPaymentOrder;

/// <summary>
/// UC48 — GET /api/v1/payments/orders/{paymentOrderId}
/// Màn hình chờ thanh toán poll trạng thái cho tới khi cổng xác nhận.
/// Nếu webhook chưa tới, endpoint này CHỦ ĐỘNG đối chiếu với cổng.
/// </summary>
public record GetPaymentOrderQuery(Guid UserId, Guid PaymentOrderId)
    : IRequest<(bool Success, PaymentOrderDetailDto? Data, string[] Errors)>;

public class GetPaymentOrderQueryHandler
    : IRequestHandler<GetPaymentOrderQuery, (bool, PaymentOrderDetailDto?, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly IPaymentSettlementService _settlement;
    private readonly ILogger<GetPaymentOrderQueryHandler> _logger;

    public GetPaymentOrderQueryHandler(
        IApplicationDbContext db,
        IPaymentGateway gateway,
        IPaymentSettlementService settlement,
        ILogger<GetPaymentOrderQueryHandler> logger)
    {
        _db = db;
        _gateway = gateway;
        _settlement = settlement;
        _logger = logger;
    }

    public async Task<(bool, PaymentOrderDetailDto?, string[])> Handle(
        GetPaymentOrderQuery request,
        CancellationToken cancellationToken)
    {
        var order = await _db.PaymentOrders
            .FirstOrDefaultAsync(
                o => o.Id == request.PaymentOrderId && o.UserId == request.UserId && !o.IsDeleted,
                cancellationToken);

        if (order is null)
        {
            return (false, null, ["Không tìm thấy đơn nạp tiền."]);
        }

        // Webhook có thể chưa tới (dev local không có ngrok, hoặc payOS retry)
        // => chủ động hỏi cổng để không bắt người dùng chờ vô hạn.
        if (order.Status == PaymentOrderStatus.Pending)
        {
            await TryReconcileWithGatewayAsync(order.Id, order.PayOsOrderCode, cancellationToken);

            order = await _db.PaymentOrders
                .AsNoTracking()
                .FirstAsync(o => o.Id == request.PaymentOrderId, cancellationToken);
        }

        var wallet = await _db.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == request.UserId && !w.IsDeleted, cancellationToken);

        return (true, new PaymentOrderDetailDto(
            PaymentOrderId: order.Id,
            OrderRef: order.OrderRef,
            Amount: order.Amount,
            Gateway: order.Gateway.ToString(),
            PaymentOrderStatus: order.Status.ToString(),
            PaidAt: order.PaidAt,
            WalletBalance: wallet?.Balance ?? 0m,
            PaymentUrl: order.CheckoutUrl,
            GatewayOrderCode: order.PayOsOrderCode
        ), []);
    }

    private async Task TryReconcileWithGatewayAsync(
        Guid orderId,
        long orderCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var link = await _gateway.GetPaymentLinkAsync(orderCode, cancellationToken);

            // Chỉ xử lý khi cổng báo đã trả ĐỦ tiền.
            // UNDERPAID / PENDING / PROCESSING => còn chờ, không cộng tiền.
            if (link.Status == "PAID" && link.AmountPaid >= link.Amount)
            {
                var transactionRef = link.Transactions?.FirstOrDefault()?.Reference;

                var outcome = await _settlement.ApplyPaidOrderAsync(
                    orderId,
                    transactionRef,
                    link.AmountPaid,
                    DateTimeOffset.UtcNow,
                    source: "poll",
                    cancellationToken);

                _logger.LogInformation(
                    "Đối chiếu đơn {OrderId} với payOS: applied={Applied}, reason={Reason}",
                    orderId, outcome.Applied, outcome.Reason);
            }
            else if (link.Status is "CANCELLED" or "EXPIRED" or "FAILED")
            {
                var order = await _db.PaymentOrders
                    .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

                if (order is not null && order.Status == PaymentOrderStatus.Pending)
                {
                    order.Status = link.Status switch
                    {
                        "CANCELLED" => PaymentOrderStatus.Cancelled,
                        "EXPIRED" => PaymentOrderStatus.Expired,
                        _ => PaymentOrderStatus.Failed
                    };
                    order.FailureReason = $"Cổng báo trạng thái {link.Status} khi đối chiếu.";
                    order.UpdatedAt = DateTimeOffset.UtcNow;
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
        }
        catch (PaymentGatewayException ex)
        {
            // Đối chiếu lỗi KHÔNG được làm hỏng endpoint poll — người dùng vẫn xem được trạng thái cũ
            _logger.LogWarning(ex, "Không đối chiếu được đơn {OrderId} với payOS", orderId);
        }
    }
}
