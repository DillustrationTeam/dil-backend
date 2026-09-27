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

namespace ArtCommission.Application.Auction.Commands.SettleAuction;

/// <summary>
/// UC35 — POST /api/v1/auctions/{auctionId}/settle
/// Chốt phiên khi hết hạn: xác định winner, dùng tiền cọc đã giữ để trả người bán,
/// chuyển quyền sở hữu tranh và tạo bản ghi bàn giao file gốc.
///
/// QUYỀN GỌI: Administrator (chốt tay / job nền) hoặc chính người bán.
/// Bidder KHÔNG được tự chốt phiên — nếu không, họ có thể chốt đúng lúc giá có lợi cho mình.
/// </summary>
public record SettleAuctionCommand(
    Guid UserId,
    Guid AuctionId,
    bool IsAdministrator = false,
    bool Force = false
) : IRequest<(bool Success, AuctionSettlementDto? Data, string[] Errors)>;

public class SettleAuctionCommandHandler
    : IRequestHandler<SettleAuctionCommand, (bool, AuctionSettlementDto?, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly IAuctionSettlementService _settlementService;
    private readonly IAuctionMoneyService _moneyService;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<SettleAuctionCommandHandler> _logger;

    public SettleAuctionCommandHandler(
        IApplicationDbContext db,
        IAuctionSettlementService settlementService,
        IAuctionMoneyService moneyService,
        INotificationPublisher notifications,
        ILogger<SettleAuctionCommandHandler> logger)
    {
        _db = db;
        _settlementService = settlementService;
        _moneyService = moneyService;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<(bool, AuctionSettlementDto?, string[])> Handle(
        SettleAuctionCommand request,
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
            return (false, null, ["Chỉ người bán hoặc quản trị viên mới được chốt phiên."]);
        }

        var now = DateTimeOffset.UtcNow;

        // ĐÃ CHỐT: trả lại kết quả cũ. Đây là chốt chặn idempotent quan trọng nhất —
        // job nền và Admin có thể cùng gọi settle cho một phiên.
        if (auction.Status == AuctionStatus.Settled && auction.SettledAt.HasValue)
        {
            return await BuildIdempotentResponseAsync(auction, cancellationToken);
        }

        if (auction.Status == AuctionStatus.Cancelled)
        {
            return (false, null, ["Phiên đã bị huỷ nên không chốt được."]);
        }

        // Chưa tới giờ kết thúc: chỉ Admin với force=true được chốt sớm.
        if (auction.EndAt > now && !(request.IsAdministrator && request.Force))
        {
            return (false, null, ["Phiên chưa kết thúc. Chỉ quản trị viên có thể chốt sớm."]);
        }

        // Lấy bid cao nhất còn hiệu lực. Xếp theo Amount rồi PlacedAt để kết quả
        // tất định khi hai bid cùng giá (người đặt trước thắng).
        var winningBid = await _db.Bids
            .Where(b => b.AuctionId == auction.Id
                        && !b.IsDeleted
                        && (b.Status == BidStatus.Leading || b.Status == BidStatus.Won))
            .OrderByDescending(b => b.Amount)
            .ThenBy(b => b.PlacedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (winningBid is null)
        {
            // Không có bid nào: phiên kết thúc vô chủ, đánh dấu Ended và ghi lý do.
            auction.Status = AuctionStatus.Ended;
            auction.CancelReason ??= "Kết thúc phiên không có lượt đặt giá hợp lệ.";
            auction.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return (false, null, ["Phiên không có lượt đặt giá hợp lệ nên không có người thắng."]);
        }

        // Giá sàn bí mật: có bid nhưng chưa đạt giá sàn thì KHÔNG bán.
        if (auction.ReservePrice.HasValue && winningBid.Amount < auction.ReservePrice.Value)
        {
            auction.Status = AuctionStatus.Ended;
            auction.CancelReason ??=
                $"Giá cao nhất {winningBid.Amount:N0} chưa đạt giá sàn {auction.ReservePrice.Value:N0}.";
            auction.UpdatedAt = now;

            // Nhả cọc cho người đã đặt giá — họ không có lỗi gì.
            //
            // LỖI ĐÃ SỬA: trước đây nhánh này chỉ đặt Status = Lost rồi commit mà KHÔNG
            // gọi RefundDepositAsync ⇒ HoldStatus giữ nguyên Held và số tiền cọc nằm
            // vĩnh viễn trong LockedBalance của bidder (không ai giải phóng được nữa,
            // vi phạm bất biến của IAuctionMoneyService: mọi bid thôi dẫn đầu phải
            // được Refunded/Released).
            winningBid.Status = BidStatus.Lost;
            winningBid.UpdatedAt = now;

            await _moneyService.RefundDepositAsync(
                winningBid, "Phiên không bán được vì chưa đạt giá sàn.", cancellationToken);

            // Nhả luôn cọc của các bid thua khác — nếu chỉ nhả bid cao nhất thì các
            // bid còn lại vẫn Held và tiền tiếp tục mắc kẹt.
            var otherHeldBids = await _db.Bids
                .Where(b => b.AuctionId == auction.Id
                            && !b.IsDeleted
                            && b.Id != winningBid.Id
                            && b.HoldStatus == HoldStatus.Held)
                .ToListAsync(cancellationToken);

            foreach (var heldBid in otherHeldBids)
            {
                heldBid.Status = BidStatus.Lost;
                heldBid.UpdatedAt = now;

                await _moneyService.RefundDepositAsync(
                    heldBid, "Phiên không bán được vì chưa đạt giá sàn.", cancellationToken);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return (false, null,
                [$"Giá cao nhất chưa đạt giá sàn {auction.ReservePrice.Value:N0} VND nên phiên không bán được."]);
        }

        var result = await _settlementService.SettleWithHeldDepositAsync(auction, winningBid, cancellationToken);

        // Không chốt được (ví dụ người mua không đủ số dư để bù phần cọc thiếu).
        // Phải rollback vì dịch vụ đã nhả cọc TRƯỚC khi phát hiện lỗi.
        if (!result.HasWinner)
        {
            await tx.RollbackAsync(cancellationToken);

            return (false, null,
                ["Không chốt được phiên: người thắng không đủ số dư để hoàn tất thanh toán."]);
        }

        // Nhả cọc cho TẤT CẢ các bid thua còn đang giữ tiền.
        // Không làm bước này thì tiền cọc nằm chết trong LockedBalance của người thua.
        var losingBids = await _db.Bids
            .Where(b => b.AuctionId == auction.Id
                        && !b.IsDeleted
                        && b.Id != winningBid.Id
                        && b.HoldStatus == HoldStatus.Held)
            .ToListAsync(cancellationToken);

        foreach (var losingBid in losingBids)
        {
            losingBid.Status = BidStatus.Lost;
            losingBid.UpdatedAt = now;

            await _moneyService.RefundDepositAsync(
                losingBid, "Phiên đã chốt — bạn không thắng.", cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Chốt phiên {AuctionId} bởi userId={UserId} (admin={IsAdmin}): winner={WinnerId}, final={Final}.",
            auction.Id, request.UserId, request.IsAdministrator, result.WinnerId, result.FinalPrice);

        // Thông báo cho người thua — sau commit để không báo việc không xảy ra.
        foreach (var losingBid in losingBids)
        {
            if (losingBid.BidderId == result.WinnerId)
            {
                continue;
            }

            try
            {
                await _notifications.PublishAsync(
                    losingBid.BidderId,
                    NotificationType.OutbidAlert,
                    "Phiên đấu giá đã kết thúc",
                    $"Bạn không thắng phiên này. Tiền cọc {losingBid.HoldAmount:N0} VND đã được hoàn về ví.",
                    nameof(Domain.Entities.Auction.Auction),
                    auction.Id,
                    NotificationChannel.InApp,
                    $"AuctionLost:{auction.Id}:{losingBid.BidderId}",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Không phát được thông báo thua phiên cho userId={UserId}, auctionId={AuctionId}.",
                    losingBid.BidderId, auction.Id);
            }
        }

        return (true, new AuctionSettlementDto(
            result.AuctionId,
            result.WinnerId,
            result.FinalPrice,
            result.SettledAt,
            result.ArtworkOwnershipId,
            result.EscrowTransactionId), []);
    }

    /// <summary>
    /// Trả về kết quả của phiên đã chốt. Không đụng tới tiền hay quyền sở hữu —
    /// đây là nhánh bảo đảm settle idempotent.
    /// </summary>
    private async Task<(bool, AuctionSettlementDto?, string[])> BuildIdempotentResponseAsync(
        Domain.Entities.Auction.Auction auction,
        CancellationToken cancellationToken)
    {
        var ownership = await _db.ArtworkOwnerships
            .AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.AuctionId == auction.Id && o.IsCurrent && !o.IsDeleted,
                cancellationToken);

        var escrow = await _db.EscrowTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.AuctionId == auction.Id && !e.IsDeleted, cancellationToken);

        return (true, new AuctionSettlementDto(
            auction.Id,
            auction.WinnerId,
            auction.FinalPrice ?? 0m,
            auction.SettledAt ?? DateTimeOffset.UtcNow,
            ownership?.Id,
            escrow?.Id), []);
    }
}
