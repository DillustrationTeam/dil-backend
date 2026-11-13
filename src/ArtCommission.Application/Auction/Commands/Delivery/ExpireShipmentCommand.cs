using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using INotificationPublisher = ArtCommission.Application.Notifications.Common.INotificationPublisher;

namespace ArtCommission.Application.Auction.Commands.Delivery;

/// <summary>
/// Huỷ đơn giao tranh vật lý khi NGƯỜI BÁN không gửi hàng trong hạn.
///
/// Trước đây hệ thống chỉ có hai worker: tự xác nhận sau 48 giờ kể từ lúc GỬI hàng, và huỷ
/// phiên khi người mua không chọn hình thức nhận. Thiếu đúng nhánh "người bán im lặng" nên
/// đơn nằm ở AwaitingShipment vĩnh viễn: tiền của người mua bị giữ ở sàn, không ai đóng được
/// đơn, người mua cũng không tải được file gốc.
///
/// Xử lý: hoàn tiền cho người mua, huỷ đơn + huỷ phiên, trả quyền sở hữu tranh về người bán.
/// Chạy từ worker nên KHÔNG cộng bậc phạt: quá hạn giao hàng không phải là một vụ khiếu nại
/// đã được Ban quản trị xác nhận (khác với luồng phân xử tranh chấp).
/// </summary>
public record ExpireShipmentCommand(Guid AuctionId)
    : IRequest<(bool Success, string[] Errors)>;

public class ExpireShipmentCommandHandler
    : IRequestHandler<ExpireShipmentCommand, (bool, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly IWalletService _walletService;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<ExpireShipmentCommandHandler> _logger;

    public ExpireShipmentCommandHandler(
        IApplicationDbContext db,
        IWalletService walletService,
        INotificationPublisher notifications,
        ILogger<ExpireShipmentCommandHandler> logger)
    {
        _db = db;
        _walletService = walletService;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<(bool, string[])> Handle(
        ExpireShipmentCommand request,
        CancellationToken cancellationToken)
    {
        var auction = await _db.Auctions
            .FirstOrDefaultAsync(a => a.Id == request.AuctionId && !a.IsDeleted, cancellationToken);

        if (auction is null)
        {
            return (false, ["Không tìm thấy phiên đấu giá."]);
        }

        if (auction.WinnerId is null)
        {
            return (false, ["Phiên chưa có người thắng."]);
        }

        var delivery = await _db.Set<AuctionDelivery>()
            .FirstOrDefaultAsync(d => d.AuctionId == auction.Id && !d.IsDeleted, cancellationToken);

        if (delivery is null)
        {
            return (false, ["Phiên không có đơn giao nhận."]);
        }

        if (delivery.Status != DeliveryStatus.AwaitingShipment)
        {
            return (false,
                [$"Đơn giao nhận đang ở trạng thái {delivery.Status}, không phải chờ gửi hàng."]);
        }

        var escrow = await _db.EscrowTransactions
            .FirstOrDefaultAsync(e => e.AuctionId == auction.Id && !e.IsDeleted, cancellationToken);

        if (escrow is null)
        {
            return (false, ["Không tìm thấy bản ghi ký quỹ của phiên."]);
        }

        if (escrow.Status != EscrowStatus.Deposited)
        {
            return (false, [$"Ký quỹ đang ở trạng thái {escrow.Status}, không thể hoàn tiền tự động."]);
        }

        var buyerId = auction.WinnerId.Value;
        var now = DateTimeOffset.UtcNow;
        var refundAmount = escrow.Amount - escrow.ReleasedAmount;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            if (refundAmount > 0m)
            {
                // Idempotent: worker chạy lại không được cộng tiền hai lần.
                var alreadyRefunded = await _db.WalletTransactions
                    .AnyAsync(t => t.RefId == auction.Id
                                   && t.Type == WalletTransactionType.Refund
                                   && !t.IsDeleted,
                        cancellationToken);

                if (!alreadyRefunded)
                {
                    var buyerWallet = await _walletService.GetOrCreateWalletAsync(buyerId, cancellationToken);
                    await _walletService.CreditAsync(
                        buyerWallet,
                        WalletTransactionType.Refund,
                        refundAmount,
                        AuctionRefTypes.Settlement,
                        auction.Id,
                        $"Hoàn tiền do người bán không gửi hàng đúng hạn — phiên {auction.Id}",
                        cancellationToken);
                }
            }

            escrow.Status = EscrowStatus.Refunded;
            escrow.ReleasedAmount = 0m;
            escrow.RefundedAmount = refundAmount;
            escrow.RefundedAt = now;
            escrow.Note = "Người bán không gửi hàng trong hạn — hoàn tiền cho người mua và huỷ phiên.";
            escrow.UpdatedAt = now;

            delivery.Status = DeliveryStatus.Cancelled;
            delivery.UpdatedAt = now;

            // Trả quyền sở hữu về người bán để tranh có thể đăng bán lại.
            var buyerOwnership = await _db.ArtworkOwnerships
                .FirstOrDefaultAsync(o => o.ArtworkId == auction.ArtworkId
                                          && o.OwnerId == buyerId
                                          && o.IsCurrent
                                          && !o.IsDeleted,
                    cancellationToken);

            if (buyerOwnership is not null)
            {
                buyerOwnership.IsCurrent = false;
                buyerOwnership.ReleasedAt = now;
                buyerOwnership.UpdatedAt = now;
                await _db.SaveChangesAsync(cancellationToken);
            }

            var sellerOwnership = await _db.ArtworkOwnerships
                .FirstOrDefaultAsync(o => o.ArtworkId == auction.ArtworkId
                                          && o.OwnerId == auction.SellerId
                                          && !o.IsDeleted,
                    cancellationToken);

            if (sellerOwnership is null)
            {
                _db.ArtworkOwnerships.Add(new ArtworkOwnership
                {
                    ArtworkId = auction.ArtworkId,
                    OwnerId = auction.SellerId,
                    AcquiredAt = now,
                    TransferReason = OwnershipTransferReason.AdminAdjustment,
                    AuctionId = auction.Id,
                    IsCurrent = true,
                });
            }
            else
            {
                sellerOwnership.IsCurrent = true;
                sellerOwnership.ReleasedAt = null;
                sellerOwnership.UpdatedAt = now;
            }

            var deliverable = await _db.Deliverables
                .FirstOrDefaultAsync(d => d.AuctionId == auction.Id && !d.IsDeleted, cancellationToken);

            if (deliverable is not null)
            {
                deliverable.RecipientId = auction.SellerId;
                deliverable.IsDelivered = false;
                deliverable.UpdatedAt = now;
            }

            auction.Status = AuctionStatus.Cancelled;
            auction.CancelReason = "Người bán không gửi hàng trong thời hạn — hoàn tiền cho người mua.";
            auction.RequiresPhysicalDelivery = null;
            auction.UpdatedAt = now;

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var code = auction.Id.ToString()[..8].ToUpperInvariant();

        try
        {
            await _notifications.PublishAsync(
                userId: buyerId,
                notificationType: NotificationType.AuctionLifecycle,
                title: "Đơn giao tranh đã huỷ — tiền đã hoàn về ví",
                body: $"Người bán không gửi hàng trong thời hạn nên phiên #{code} đã huỷ. "
                      + $"{refundAmount:N0} ₫ đã được hoàn về ví của bạn. Bạn có thể đánh giá người bán.",
                refType: "Auction",
                refId: auction.Id,
                channel: NotificationChannel.InApp,
                dedupKey: $"ShipmentExpired:buyer:{auction.Id}",
                cancellationToken: cancellationToken);

            await _notifications.PublishAsync(
                userId: auction.SellerId,
                notificationType: NotificationType.AuctionLifecycle,
                title: "Phiên bị huỷ vì chưa gửi hàng",
                body: $"Bạn không cập nhật vận đơn cho phiên #{code} trong thời hạn nên phiên đã huỷ, "
                      + "tiền hoàn về người mua và tác phẩm trở lại quyền sở hữu của bạn.",
                refType: "Auction",
                refId: auction.Id,
                channel: NotificationChannel.InApp,
                dedupKey: $"ShipmentExpired:seller:{auction.Id}",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot publish shipment-expiry notifications for auction {AuctionId}", auction.Id);
        }

        _logger.LogInformation(
            "Shipment expired for auction {AuctionId}: refunded {Refund:N0} to buyer and cancelled the session.",
            auction.Id, refundAmount);

        return (true, []);
    }
}
