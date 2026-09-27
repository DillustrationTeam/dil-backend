using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// MediatR cũng có INotificationPublisher — alias để không lấy nhầm của thư viện.
using INotificationPublisher = ArtCommission.Application.Notifications.Common.INotificationPublisher;

namespace ArtCommission.Application.Auction.Commands.ExpireSettlement;

/// <summary>
/// UC35 — POST /api/v1/auctions/{auctionId}/settlement/expire
/// Xử lý winner quá <c>paymentDeadline</c> không thanh toán:
///   - vô hiệu hoá kết quả (auction chuyển Expired),
///   - nhả tiền cọc còn đang giữ về ví winner,
///   - đề xuất bidder kế tiếp để người bán có phương án bán tiếp.
///
/// IDEMPOTENT: gọi lần hai trên phiên đã Expired trả lại kết quả cũ với
/// <c>ReleasedHoldAmount = 0</c>, không nhả tiền lần nữa.
/// </summary>
public record ExpireAuctionSettlementCommand(
    Guid UserId,
    Guid AuctionId,
    bool IsAdministrator = false
) : IRequest<(bool Success, AuctionExpireResultDto? Data, string[] Errors)>;

public class ExpireAuctionSettlementCommandHandler
    : IRequestHandler<ExpireAuctionSettlementCommand, (bool, AuctionExpireResultDto?, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly IAuctionMoneyService _moneyService;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<ExpireAuctionSettlementCommandHandler> _logger;

    public ExpireAuctionSettlementCommandHandler(
        IApplicationDbContext db,
        IAuctionMoneyService moneyService,
        INotificationPublisher notifications,
        ILogger<ExpireAuctionSettlementCommandHandler> logger)
    {
        _db = db;
        _moneyService = moneyService;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<(bool, AuctionExpireResultDto?, string[])> Handle(
        ExpireAuctionSettlementCommand request,
        CancellationToken cancellationToken)
    {
        if (request.AuctionId == Guid.Empty)
        {
            return (false, null, ["Thiếu mã phiên đấu giá."]);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var auction = await _db.Auctions.FirstOrDefaultAsync(
            a => a.Id == request.AuctionId && !a.IsDeleted,
            cancellationToken);

        if (auction is null)
        {
            return (false, null, ["Không tìm thấy phiên đấu giá."]);
        }

        if (!request.IsAdministrator && auction.SellerId != request.UserId)
        {
            return (false, null, ["Chỉ người bán hoặc quản trị viên mới được xử lý quá hạn."]);
        }

        // Nhánh idempotent: đã xử lý quá hạn rồi.
        if (auction.Status == AuctionStatus.Expired)
        {
            return (true, new AuctionExpireResultDto(auction.Id, null, 0m, null), []);
        }

        if (auction.Status != AuctionStatus.Settled)
        {
            return (false, null,
                [$"Chỉ xử lý quá hạn cho phiên đã chốt (trạng thái hiện tại: {AuctionMapper.DescribeStatus(auction.Status)})."]);
        }

        var now = DateTimeOffset.UtcNow;

        if (!auction.PaymentDeadline.HasValue)
        {
            return (false, null, ["Phiên chưa có hạn thanh toán nên chưa xử lý quá hạn được."]);
        }

        // Quản trị viên được xử lý sớm; người bán chỉ được xử lý khi thực sự quá hạn.
        if (!request.IsAdministrator && auction.PaymentDeadline.Value > now)
        {
            return (false, null,
                [$"Chưa quá hạn thanh toán. Hạn là {auction.PaymentDeadline.Value:yyyy-MM-dd HH:mm} UTC."]);
        }

        // Winner hiện tại = người thắng phiên đã chốt.
        var winnerBid = await _db.Bids
            .FirstOrDefaultAsync(
                b => b.AuctionId == auction.Id
                     && b.BidderId == auction.WinnerId
                     && !b.IsDeleted,
                cancellationToken);

        decimal releasedHold = 0m;

        if (winnerBid is not null)
        {
            var holdBefore = winnerBid.HoldAmount;

            // Nhả cọc nếu còn đang giữ. Sau settle, cọc của winner đã được dùng để trả
            // người bán nên HoldStatus thường đã là Released ⇒ hàm trả null và không
            // nhả hai lần. Chỉ trường hợp cọc còn Held mới thực sự hoàn tiền.
            var refunded = await _moneyService.RefundDepositAsync(
                winnerBid,
                "Winner quá hạn thanh toán — hoàn cọc.",
                cancellationToken);

            if (refunded is not null)
            {
                releasedHold = holdBefore;
            }

            winnerBid.Status = BidStatus.Expired;
            winnerBid.UpdatedAt = now;
        }

        // Đề xuất bidder kế tiếp: bid cao nhất chưa từng được chọn làm winner.
        var nextBid = await _db.Bids
            .Where(b => b.AuctionId == auction.Id
                        && !b.IsDeleted
                        && b.BidderId != auction.WinnerId)
            .OrderByDescending(b => b.Amount)
            .ThenBy(b => b.PlacedAt)
            .FirstOrDefaultAsync(cancellationToken);

        auction.Status = AuctionStatus.Expired;
        auction.CancelReason = "Người thắng không thanh toán trong hạn.";
        auction.UpdatedAt = now;

        // ------------------------------------------------------------------
        // HOÀN QUYỀN SỞ HỮU VỀ NGƯỜI BÁN.
        //
        // Lúc chốt phiên, quyền sở hữu đã được chuyển sang winner. Nếu chỉ đổi
        // auction sang Expired mà không trả tranh lại, hệ thống rơi vào trạng thái
        // mâu thuẫn: kết quả phiên bị vô hiệu nhưng tranh vẫn thuộc người đã mất quyền.
        // Người bán không thể niêm yết lại tranh của chính mình.
        //
        // Cách làm: tắt dòng IsCurrent của winner rồi mở lại (hoặc tạo) dòng của seller.
        // Tắt trước, thêm sau — unique filtered index chỉ cho một dòng IsCurrent mỗi tranh.
        // ------------------------------------------------------------------
        var currentOwnerships = await _db.ArtworkOwnerships
            .Where(o => o.ArtworkId == auction.ArtworkId && o.IsCurrent && !o.IsDeleted)
            .ToListAsync(cancellationToken);

        var alreadySellerOwned = currentOwnerships.Any(o => o.OwnerId == auction.SellerId);

        if (!alreadySellerOwned)
        {
            foreach (var ownership in currentOwnerships)
            {
                ownership.IsCurrent = false;
                ownership.ReleasedAt = now;
                ownership.UpdatedAt = now;
            }

            await _db.SaveChangesAsync(cancellationToken);

            _db.ArtworkOwnerships.Add(new Domain.Entities.Auction.ArtworkOwnership
            {
                ArtworkId = auction.ArtworkId,
                OwnerId = auction.SellerId,
                AcquiredAt = now,
                TransferReason = OwnershipTransferReason.AdminAdjustment,
                AuctionId = auction.Id,
                IsCurrent = true
            });
        }

        // Bản ghi bàn giao file gốc không còn hiệu lực: winner đã mất quyền nhận file.
        // Giữ lại dòng để tra soát nhưng đánh dấu chưa bàn giao.
        var deliverable = await _db.Deliverables.FirstOrDefaultAsync(
            d => d.AuctionId == auction.Id && !d.IsDeleted,
            cancellationToken);

        if (deliverable is not null)
        {
            deliverable.IsDelivered = false;
            deliverable.UpdatedAt = now;
        }

        // Đóng escrow: giữ lại dấu vết để đối soát.
        var escrow = await _db.EscrowTransactions.FirstOrDefaultAsync(
            e => e.AuctionId == auction.Id && !e.IsDeleted,
            cancellationToken);

        if (escrow is not null)
        {
            // Nếu cọc CHƯA từng được nhả thì hoàn được ⇒ Refunded.
            // Nếu tiền đã chuyển cho người bán ở bước chốt phiên thì KHÔNG thể tự hoàn
            // (người bán có thể đã rút) ⇒ đánh dấu Disputed để Admin đối soát tay,
            // thay vì ghi "đã hoàn" trong khi thực tế tiền chưa về ví winner.
            escrow.Status = releasedHold > 0m ? EscrowStatus.Refunded : EscrowStatus.Disputed;
            escrow.RefundedAmount = releasedHold;
            escrow.RefundedAt = releasedHold > 0m ? now : null;
            escrow.Note = releasedHold > 0m
                ? "Winner quá hạn thanh toán — đã hoàn cọc, kết quả phiên bị vô hiệu."
                : "Winner quá hạn thanh toán — kết quả vô hiệu. Tiền đã giải ngân cho người bán ở bước chốt phiên, cần Admin đối soát.";
            escrow.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Xử lý quá hạn phiên {AuctionId}: winner={WinnerId}, hoàn cọc={Released}, nextBidder={Next}.",
            auction.Id, auction.WinnerId, releasedHold, nextBid?.BidderId);

        // Thông báo sau commit.
        await NotifyQuietlyAsync(
            auction.WinnerId,
            "Phiên đấu giá đã hết hạn thanh toán",
            "Bạn không thanh toán trong thời hạn nên kết quả phiên bị huỷ." +
            (releasedHold > 0m ? $" Tiền cọc {releasedHold:N0} VND đã được hoàn về ví." : string.Empty),
            $"AuctionExpired:{auction.Id}:{auction.WinnerId}",
            auction.Id,
            cancellationToken);

        if (nextBid is not null)
        {
            await NotifyQuietlyAsync(
                nextBid.BidderId,
                "Bạn có thể là người mua kế tiếp",
                $"Người thắng phiên đã không thanh toán. Bạn là người đặt giá cao tiếp theo với {nextBid.Amount:N0} VND.",
                $"AuctionNextBidder:{auction.Id}:{nextBid.BidderId}",
                auction.Id,
                cancellationToken);
        }

        return (true, new AuctionExpireResultDto(
            auction.Id,
            winnerBid?.Id,
            releasedHold,
            nextBid?.BidderId), []);
    }

    private async Task NotifyQuietlyAsync(
        Guid? userId,
        string title,
        string body,
        string dedupKey,
        Guid auctionId,
        CancellationToken cancellationToken)
    {
        if (!userId.HasValue)
        {
            return;
        }

        try
        {
            await _notifications.PublishAsync(
                userId.Value,
                NotificationType.AuctionEndingSoon,
                title,
                body,
                nameof(Domain.Entities.Auction.Auction),
                auctionId,
                NotificationChannel.InApp,
                dedupKey,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Không phát được thông báo quá hạn cho userId={UserId}, auctionId={AuctionId}.",
                userId.Value, auctionId);
        }
    }
}
