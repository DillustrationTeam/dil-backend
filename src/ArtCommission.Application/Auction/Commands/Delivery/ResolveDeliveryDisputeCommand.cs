using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Auction.DTOs;
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
/// Admin phân xử khiếu nại giao nhận tranh vật lý.
///
/// Trước đây dispute chỉ đóng băng ký quỹ mà KHÔNG có đường ra: không ai giải ngân được,
/// cũng không ai hoàn tiền được, tiền treo vĩnh viễn. Command này là lối thoát duy nhất,
/// và chỉ Admin gọi được.
///
/// Hai hướng xử lý:
///   - <c>release</c>: bưu phẩm/hư hại không đủ căn cứ — giải ngân nốt cho người bán, phiên hoàn tất.
///   - <c>refund</c>: chấp nhận khiếu nại — hoàn tiền ký quỹ về ví người mua, huỷ phiên và
///     trả quyền sở hữu tranh về cho người bán.
///   - <c>close</c>: đóng khiếu nại khi ký quỹ ĐÃ giải ngân (không còn quỹ để hoàn). Cả hai bên
///     nhận thông báo, người mua mở khoá quyền đánh giá người bán.
///
/// Gọi <c>refund</c> trên phiên đã giải ngân KHÔNG phải lỗi: hệ thống tự chuyển sang <c>close</c>,
/// vì tiền đã về ví người bán thì không thu hồi được nữa — báo lỗi chỉ làm Admin bế tắc.
/// </summary>
public record ResolveDeliveryDisputeCommand(
    Guid AdminUserId,
    Guid AuctionId,
    string Decision,
    string? Note = null
) : IRequest<(bool Success, AuctionDeliveryDto? Data, string[] Errors)>;

public class ResolveDeliveryDisputeCommandHandler
    : IRequestHandler<ResolveDeliveryDisputeCommand, (bool, AuctionDeliveryDto?, string[])>
{
    public const string ReleaseDecision = "release";
    public const string RefundDecision = "refund";
    public const string CloseDecision = "close";

    /// <summary>Mã kết quả lưu vào <c>AuctionDelivery.DisputeResolution</c> khi không hoàn được tiền.</summary>
    public const string PaidOutResolution = "paid-out";

    private readonly IApplicationDbContext _db;
    private readonly IAuctionSettlementService _settlementService;
    private readonly IWalletService _walletService;
    private readonly IAuctionPenaltyService _penaltyService;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<ResolveDeliveryDisputeCommandHandler> _logger;

    public ResolveDeliveryDisputeCommandHandler(
        IApplicationDbContext db,
        IAuctionSettlementService settlementService,
        IWalletService walletService,
        IAuctionPenaltyService penaltyService,
        INotificationPublisher notifications,
        ILogger<ResolveDeliveryDisputeCommandHandler> logger)
    {
        _db = db;
        _settlementService = settlementService;
        _walletService = walletService;
        _penaltyService = penaltyService;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<(bool, AuctionDeliveryDto?, string[])> Handle(
        ResolveDeliveryDisputeCommand request,
        CancellationToken cancellationToken)
    {
        var decision = request.Decision?.Trim().ToLowerInvariant();
        if (decision is not (ReleaseDecision or RefundDecision or CloseDecision))
        {
            return (false, null, ["Quyết định không hợp lệ. Chỉ nhận 'release', 'refund' hoặc 'close'."]);
        }

        var auction = await _db.Auctions
            .FirstOrDefaultAsync(a => a.Id == request.AuctionId && !a.IsDeleted, cancellationToken);

        if (auction is null)
        {
            return (false, null, ["Không tìm thấy phiên đấu giá."]);
        }

        var delivery = await _db.Set<AuctionDelivery>()
            .FirstOrDefaultAsync(d => d.AuctionId == auction.Id && !d.IsDeleted, cancellationToken);

        if (delivery is null)
        {
            return (false, null, ["Phiên này không có bản ghi giao nhận."]);
        }

        var escrow = await _db.EscrowTransactions
            .FirstOrDefaultAsync(e => e.AuctionId == auction.Id && !e.IsDeleted, cancellationToken);

        if (escrow is null)
        {
            return (false, null, ["Không tìm thấy bản ghi ký quỹ của phiên."]);
        }

        // Chỉ phân xử khi thực sự đang có tranh chấp — tránh Admin vô tình giải ngân/hoàn tiền
        // cho một phiên đang chạy bình thường.
        if (delivery.Status is not DeliveryStatus.Disputed && escrow.Status is not EscrowStatus.Disputed)
        {
            return (false, null, ["Phiên này không ở trạng thái khiếu nại nên không cần phân xử."]);
        }

        var now = DateTimeOffset.UtcNow;
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        // Remember what the admin actually decided: a refund on an already released escrow is
        // downgraded to "close" below, but the buyer still won the dispute and the seller still
        // earns a penalty step.
        var buyerWonDispute = decision == RefundDecision;
        var escrowWasReleased = escrow.Status == EscrowStatus.Released;

        // Tiền đã về ví người bán thì không đòi lại được — hạ "refund" xuống "close" thay vì trả
        // lỗi, để Admin vẫn đóng được khiếu nại và hai bên đều nhận thông báo rõ ràng.
        if (decision == RefundDecision && escrow.Status == EscrowStatus.Released)
        {
            decision = CloseDecision;
        }

        // Toàn bộ phân xử (cộng ví + escrow + quyền sở hữu + trạng thái phiên) phải nằm trong
        // MỘT transaction. Trước đây WalletService.CreditAsync tự SaveChanges nên khi bước sau
        // lỗi, người mua đã nhận tiền nhưng escrow/quyền sở hữu không đổi; bấm lại thì chết vì
        // trùng khoá unique UX_WalletTransaction_Ref (AuctionSettlement, auctionId, Refund).
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        if (decision == ReleaseDecision)
        {
            var (releaseOk, releaseErrors) = await _settlementService
                .ReleaseEscrowToSellerAsync(auction, cancellationToken, allowDisputed: true);

            if (!releaseOk)
            {
                return (false, null, releaseErrors);
            }

            delivery.Status = DeliveryStatus.Delivered;
            delivery.DeliveredAt ??= now;
            delivery.ConfirmedAt = now;
            delivery.DisputeResolution = ReleaseDecision;
            delivery.DisputeResolvedAt = now;
            delivery.DisputeResolvedNote = note;
            delivery.UpdatedAt = now;

            auction.Status = AuctionStatus.Completed;
            auction.UpdatedAt = now;
        }
        else if (decision == CloseDecision)
        {
            // Ký quỹ đã giải ngân: chỉ đóng hồ sơ, không đụng vào tiền, không đụng quyền sở hữu.
            delivery.Status = DeliveryStatus.Resolved;
            delivery.DisputeResolution = PaidOutResolution;
            delivery.DisputeResolvedAt = now;
            delivery.DisputeResolvedNote = note ?? "Ký quỹ đã giải ngân cho người bán — không thể hoàn tiền.";
            delivery.UpdatedAt = now;

            if (auction.Status != AuctionStatus.Completed)
            {
                auction.Status = AuctionStatus.Completed;
                auction.UpdatedAt = now;
            }
        }
        else
        {
            if (escrow.Status == EscrowStatus.Released)
            {
                // Đã hạ xuống CloseDecision ở trên; nhánh này chỉ còn là lưới an toàn.
                return (false, null, ["Ký quỹ đã giải ngân cho người bán — hãy dùng quyết định 'close'."]);
            }

            var buyerId = auction.WinnerId;
            if (!buyerId.HasValue)
            {
                return (false, null, ["Phiên không có người thắng để hoàn tiền."]);
            }

            var refundAmount = escrow.Amount - escrow.ReleasedAmount;
            if (refundAmount > 0m)
            {
                // Idempotent: lần bấm trước có thể đã cộng ví rồi mới lỗi ở bước sau (unique index
                // / trạng thái phiên). Nếu đã có giao dịch Refund cho phiên này thì KHÔNG cộng lần
                // hai — chỉ hoàn tất phần sổ sách còn thiếu.
                var alreadyRefunded = await _db.WalletTransactions
                    .AnyAsync(t => t.RefId == auction.Id
                                   && t.Type == WalletTransactionType.Refund
                                   && !t.IsDeleted,
                        cancellationToken);

                if (alreadyRefunded)
                {
                    _logger.LogWarning(
                        "Phiên {AuctionId} đã có giao dịch Refund trước đó — bỏ qua cộng tiền, chỉ đóng hồ sơ.",
                        auction.Id);
                }
                else
                {
                    var buyerWallet = await _walletService.GetOrCreateWalletAsync(buyerId.Value, cancellationToken);
                    await _walletService.CreditAsync(
                        buyerWallet,
                        WalletTransactionType.Refund,
                        refundAmount,
                        AuctionRefTypes.Settlement,
                        auction.Id,
                        $"Hoàn tiền ký quỹ do khiếu nại giao nhận được chấp nhận — phiên {auction.Id}",
                        cancellationToken);
                }
            }

            escrow.Status = EscrowStatus.Refunded;
            escrow.ReleasedAt = now;
            escrow.ReleasedAmount = 0m;
            escrow.RefundedAmount = refundAmount;
            escrow.RefundedAt = now;
            escrow.Note = note ?? "Hoàn tiền do khiếu nại giao nhận.";
            escrow.UpdatedAt = now;

            delivery.Status = DeliveryStatus.Cancelled;
            delivery.DisputeResolution = RefundDecision;
            delivery.DisputeResolvedAt = now;
            delivery.DisputeResolvedNote = note;
            delivery.UpdatedAt = now;

            auction.Status = AuctionStatus.Cancelled;
            auction.CancelReason = note ?? "Khiếu nại giao nhận được chấp nhận — hoàn tiền cho người mua.";
            auction.UpdatedAt = now;

            // Trả quyền sở hữu tranh về người bán: bỏ quyền hiện tại của người mua và bật lại
            // dòng sở hữu của người bán để tranh không bị mồ côi.
            var winnerOwnership = await _db.ArtworkOwnerships
                .FirstOrDefaultAsync(o => o.ArtworkId == auction.ArtworkId
                                          && o.OwnerId == buyerId.Value
                                          && o.IsCurrent
                                          && !o.IsDeleted,
                    cancellationToken);

            if (winnerOwnership is not null)
            {
                winnerOwnership.IsCurrent = false;
                winnerOwnership.ReleasedAt = now;
                winnerOwnership.UpdatedAt = now;

                // Ghi ngay trong transaction: unique index UX_ArtworkOwnerships_CurrentOwner chỉ
                // cho phép MỘT dòng IsCurrent=1 mỗi tranh, nên không thể tắt quyền người mua và
                // bật quyền người bán trong cùng một SaveChanges (thứ tự lệnh SQL không đảm bảo).
                await _db.SaveChangesAsync(cancellationToken);
            }

            var sellerOwnership = await _db.ArtworkOwnerships
                .FirstOrDefaultAsync(o => o.ArtworkId == auction.ArtworkId && o.OwnerId == auction.SellerId && !o.IsDeleted,
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
                    IsCurrent = true
                });
            }
            else
            {
                sellerOwnership.IsCurrent = true;
                sellerOwnership.ReleasedAt = null;
                sellerOwnership.UpdatedAt = now;
            }

            // File số cũng phải quay về người bán, nếu không người mua vẫn tải được bản gốc.
            var deliverable = await _db.Deliverables
                .FirstOrDefaultAsync(d => d.AuctionId == auction.Id && !d.IsDeleted, cancellationToken);
            if (deliverable is not null)
            {
                deliverable.RecipientId = auction.SellerId;
                deliverable.UpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Arbitration money and state are already committed. The penalty step is derived data:
        // a failure here must be logged, never rolled back onto the buyer's refund.
        if (buyerWonDispute)
        {
            try
            {
                await _penaltyService.ApplyConfirmedDisputeAsync(
                    auction, delivery, escrowWasReleased, request.AdminUserId, note, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Cannot apply the seller penalty ladder for auction {AuctionId} after a confirmed dispute.",
                    auction.Id);
            }
        }

        _logger.LogWarning(
            "Admin {AdminId} phân xử khiếu nại phiên {AuctionId}: {Decision}. Ghi chú: {Note}",
            request.AdminUserId, auction.Id, decision, note);

        var shortId = auction.Id.ToString()[..8].ToUpperInvariant();
        var notifiedBuyerId = auction.WinnerId;

        // Cả người mua và người bán đều phải biết kết quả — trước đây chỉ một bên nhận thông báo.
        var buyerTitle = decision switch
        {
            ReleaseDecision => "Khiếu nại bị bác — tiền đã giải ngân cho người bán",
            RefundDecision => "Khiếu nại được chấp nhận — tiền đã hoàn về ví",
            _ => "Khiếu nại đã đóng — không thể hoàn tiền"
        };

        var buyerBody = decision switch
        {
            ReleaseDecision =>
                $"Ban quản trị đã phân xử phiên #{shortId}: bằng chứng chưa đủ căn cứ, tiền được giải ngân cho người bán. Bạn vẫn có thể đánh giá người bán.",
            RefundDecision =>
                $"Ban quản trị đã chấp nhận khiếu nại phiên #{shortId}: tiền ký quỹ được hoàn về ví người mua và quyền sở hữu tranh trả về người bán.",
            _ =>
                $"Ban quản trị đã đóng khiếu nại phiên #{shortId}. Ký quỹ đã giải ngân cho người bán trước đó nên không thể hoàn tiền được nữa. Bạn có thể đánh giá người bán để cảnh báo cộng đồng."
        };

        var sellerTitle = decision switch
        {
            ReleaseDecision => "Khiếu nại bị bác — tiền đã giải ngân",
            RefundDecision => "Khiếu nại được chấp nhận — hoàn tiền cho người mua",
            _ => "Khiếu nại đã đóng — tiền giữ nguyên"
        };

        var sellerBody = decision switch
        {
            ReleaseDecision =>
                $"Phiên #{shortId}: khiếu nại của người mua bị bác, tiền bán tranh đã được giải ngân cho bạn.",
            RefundDecision =>
                $"Phiên #{shortId}: khiếu nại được chấp nhận, ký quỹ đã hoàn về người mua và quyền sở hữu tranh trở về bạn.",
            _ =>
                $"Phiên #{shortId}: khiếu nại giao nhận đã được đóng. Khoản tiền đã giải ngân cho bạn trước đó được giữ nguyên."
        };

        try
        {
            if (notifiedBuyerId.HasValue)
            {
                await _notifications.PublishAsync(
                    userId: notifiedBuyerId.Value,
                    notificationType: NotificationType.DisputeStatusChanged,
                    title: buyerTitle,
                    body: buyerBody,
                    refType: "Auction",
                    refId: auction.Id,
                    cancellationToken: cancellationToken);
            }

            await _notifications.PublishAsync(
                userId: auction.SellerId,
                notificationType: NotificationType.DisputeStatusChanged,
                title: sellerTitle,
                body: sellerBody,
                refType: "Auction",
                refId: auction.Id,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi gửi thông báo phân xử khiếu nại phiên {AuctionId}", auction.Id);
        }

        // Người mua được đánh giá người bán ngay sau khi hồ sơ khiếu nại khép lại.
        var hasRatedSeller = await _db.CreatorReviews
            .AnyAsync(r => r.AuctionId == auction.Id && !r.IsDeleted, cancellationToken);

        return (true,
            AuctionMapper.ToDeliveryDto(delivery, canRateSeller: !hasRatedSeller, hasRatedSeller: hasRatedSeller),
            []);
    }
}
