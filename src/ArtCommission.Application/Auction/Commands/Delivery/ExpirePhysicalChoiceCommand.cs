using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using INotificationPublisher = ArtCommission.Application.Notifications.Common.INotificationPublisher;

namespace ArtCommission.Application.Auction.Commands.Delivery;

/// <summary>
/// Closes an auction whose winner never answered the 48-hour physical-delivery question.
///
/// Silence is treated as "no longer wants the artwork": the escrow goes back to the buyer and
/// the session is cancelled, so the seller keeps the artwork and the buyer keeps the money.
/// This runs from the background worker, therefore it takes no user id and never raises a
/// seller penalty — a timeout is not a confirmed delivery dispute.
/// </summary>
public record ExpirePhysicalChoiceCommand(Guid AuctionId)
    : IRequest<(bool Success, string[] Errors)>;

public class ExpirePhysicalChoiceCommandHandler
    : IRequestHandler<ExpirePhysicalChoiceCommand, (bool, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly IWalletService _walletService;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<ExpirePhysicalChoiceCommandHandler> _logger;

    public ExpirePhysicalChoiceCommandHandler(
        IApplicationDbContext db,
        IWalletService walletService,
        INotificationPublisher notifications,
        ILogger<ExpirePhysicalChoiceCommandHandler> logger)
    {
        _db = db;
        _walletService = walletService;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<(bool, string[])> Handle(
        ExpirePhysicalChoiceCommand request,
        CancellationToken cancellationToken)
    {
        var auction = await _db.Auctions
            .FirstOrDefaultAsync(a => a.Id == request.AuctionId && !a.IsDeleted, cancellationToken);

        if (auction is null)
        {
            return (false, ["Không tìm thấy phiên đấu giá."]);
        }

        if (auction.Status != AuctionStatus.AwaitingPhysical || auction.WinnerId is null)
        {
            return (false, ["Phiên không còn ở trạng thái chờ chọn hình thức nhận tranh."]);
        }

        var escrow = await _db.EscrowTransactions
            .FirstOrDefaultAsync(e => e.AuctionId == auction.Id && !e.IsDeleted, cancellationToken);

        if (escrow is null)
        {
            return (false, ["Không tìm thấy bản ghi ký quỹ của phiên."]);
        }

        if (escrow.Status == EscrowStatus.Released)
        {
            return (false, ["Ký quỹ đã giải ngân cho người bán, không thể huỷ phiên."]);
        }

        var buyerId = auction.WinnerId.Value;
        var now = DateTimeOffset.UtcNow;
        var refundAmount = escrow.Amount - escrow.ReleasedAmount;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            if (refundAmount > 0m)
            {
                // Idempotent: a retried job must not credit the buyer twice.
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
                        $"Hoàn tiền do quá hạn 48 giờ chọn hình thức nhận tranh — phiên {auction.Id}",
                        cancellationToken);
                }
            }

            escrow.Status = EscrowStatus.Refunded;
            escrow.ReleasedAt = now;
            escrow.ReleasedAmount = 0m;
            escrow.RefundedAmount = refundAmount;
            escrow.RefundedAt = now;
            escrow.Note = "Người thắng không phản hồi trong 48 giờ — hoàn tiền và huỷ phiên.";
            escrow.UpdatedAt = now;

            // Ownership goes back to the seller, the artwork is free for a new session.
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
                deliverable.UpdatedAt = now;
            }

            auction.Status = AuctionStatus.Cancelled;
            auction.CancelReason = "Người thắng không chọn hình thức nhận tranh trong 48 giờ — hoàn tiền cho người mua.";
            auction.RequiresPhysicalDelivery = false;
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
                title: "Phiên đấu giá đã huỷ — tiền đã hoàn về ví",
                body: $"Bạn không chọn hình thức nhận tranh trong 48 giờ nên phiên #{code} đã tự huỷ. "
                      + $"{refundAmount:N0} ₫ đã được hoàn về ví của bạn.",
                refType: "Auction",
                refId: auction.Id,
                channel: NotificationChannel.InApp,
                dedupKey: $"PhysicalChoiceExpired:buyer:{auction.Id}",
                cancellationToken: cancellationToken);

            await _notifications.PublishAsync(
                userId: auction.SellerId,
                notificationType: NotificationType.AuctionLifecycle,
                title: "Phiên đấu giá đã huỷ do người mua không phản hồi",
                body: $"Người thắng phiên #{code} không chọn hình thức nhận tranh trong 48 giờ. "
                      + "Phiên đã huỷ, tiền hoàn về người mua và tác phẩm trở lại quyền sở hữu của bạn.",
                refType: "Auction",
                refId: auction.Id,
                channel: NotificationChannel.InApp,
                dedupKey: $"PhysicalChoiceExpired:seller:{auction.Id}",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot publish expiry notifications for auction {AuctionId}", auction.Id);
        }

        _logger.LogInformation(
            "Physical choice expired for auction {AuctionId}: refunded {Refund:N0} to buyer and cancelled the session.",
            auction.Id, refundAmount);

        return (true, []);
    }
}
