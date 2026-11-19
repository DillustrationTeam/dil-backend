using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Auction.Commands.DeleteAuction;

/// <summary>
/// UC32 — DELETE /api/v1/auctions/{auctionId}
/// Người bán huỷ phiên CHƯA phát sinh bid.
///
/// Dùng xoá mềm + đổi trạng thái Cancelled: phiên đã huỷ vẫn phải tra soát được
/// (ai huỷ, lúc nào, vì sao), nên không xoá cứng khỏi DB.
/// </summary>
public record DeleteAuctionCommand(
    Guid UserId,
    Guid AuctionId,
    string? Reason = null,
    bool IsAdministrator = false
) : IRequest<(bool Success, string[] Errors)>;

public class DeleteAuctionCommandHandler
    : IRequestHandler<DeleteAuctionCommand, (bool, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly IAuctionRealtimePublisher _realtime;
    private readonly IAuctionLifecycleNotifier _lifecycleNotifier;
    private readonly ILogger<DeleteAuctionCommandHandler> _logger;

    public DeleteAuctionCommandHandler(
        IApplicationDbContext db,
        IAuctionRealtimePublisher realtime,
        IAuctionLifecycleNotifier lifecycleNotifier,
        ILogger<DeleteAuctionCommandHandler> logger)
    {
        _db = db;
        _realtime = realtime;
        _lifecycleNotifier = lifecycleNotifier;
        _logger = logger;
    }

    public async Task<(bool, string[])> Handle(
        DeleteAuctionCommand request,
        CancellationToken cancellationToken)
    {
        if (request.AuctionId == Guid.Empty)
        {
            return (false, ["Thiếu mã phiên đấu giá."]);
        }

        var auction = await _db.Auctions.FirstOrDefaultAsync(
            a => a.Id == request.AuctionId && !a.IsDeleted,
            cancellationToken);

        if (auction is null)
        {
            return (false, ["Không tìm thấy phiên đấu giá."]);
        }

        if (!request.IsAdministrator && auction.SellerId != request.UserId)
        {
            return (false, ["Chỉ người bán hoặc quản trị viên mới được huỷ phiên này."]);
        }

        // Đã BÁN (dù qua đặt giá hay qua Mua ngay) thì không huỷ: quyền sở hữu đã chuyển,
        // tiền đã vào ký quỹ, huỷ ở đây tạo trạng thái mâu thuẫn.
        // Trước đây chỉ chặn Settled/Cancelled + BidCount > 0, nên phiên bán bằng MUA NGAY
        // (BidCount luôn = 0) đang ở AwaitingPhysical / InTransit / Completed vẫn bị người bán
        // huỷ được — tranh đã sang tên người mua mà phiên thành "đã huỷ".
        if (auction.Status is AuctionStatus.Settled
            or AuctionStatus.Cancelled
            or AuctionStatus.AwaitingPhysical
            or AuctionStatus.InTransit
            or AuctionStatus.Disputed
            or AuctionStatus.Completed
            or AuctionStatus.Expired)
        {
            return (false,
                [$"Không huỷ được phiên đang ở trạng thái {AuctionMapper.DescribeStatus(auction.Status)}."]);
        }

        // Chốt theo dữ liệu tiền/người thắng, không chỉ theo trạng thái: nếu vì lý do nào đó
        // trạng thái bị đặt lại nhưng phiên đã có người thắng hoặc đã chốt giá thì vẫn chặn.
        if (auction.WinnerId is not null || auction.SettledAt is not null || auction.FinalPrice is not null)
        {
            return (false, ["Phiên đã có người thắng nên không huỷ được."]);
        }

        if (auction.BidCount > 0)
        {
            return (false, ["Phiên đã có lượt đặt giá nên không huỷ được. Hãy để phiên kết thúc tự nhiên."]);
        }

        var now = DateTimeOffset.UtcNow;

        auction.Status = AuctionStatus.Cancelled;
        auction.CancelReason = string.IsNullOrWhiteSpace(request.Reason)
            ? "Người bán huỷ phiên."
            : request.Reason!.Trim();
        auction.UpdatedAt = now;
        // KHÔNG soft-delete: phiên đã huỷ phải hiện ở tab 'Đã huỷ' của Admin và trong lịch sử của người bán.
        // Soft-delete khiến bảng Cancelled luôn rỗng và mất dấu vết huỷ phiên.

        await _db.SaveChangesAsync(cancellationToken);

        await _lifecycleNotifier.NotifyParticipantsAsync(
            auction.Id, AuctionLifecycleEvent.Cancelled, cancellationToken: cancellationToken);
        try
        {
            await _realtime.PublishAuctionStatusChangedAsync(
                auction.Id, auction.Status.ToString(), auction.CurrentPrice, auction.EndAt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không broadcast được trạng thái huỷ auctionId={AuctionId}.", auction.Id);
        }

        return (true, []);
    }
}
