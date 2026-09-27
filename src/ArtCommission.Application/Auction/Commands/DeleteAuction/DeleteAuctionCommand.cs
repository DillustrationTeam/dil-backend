using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
    string? Reason = null
) : IRequest<(bool Success, string[] Errors)>;

public class DeleteAuctionCommandHandler
    : IRequestHandler<DeleteAuctionCommand, (bool, string[])>
{
    private readonly IApplicationDbContext _db;

    public DeleteAuctionCommandHandler(IApplicationDbContext db)
    {
        _db = db;
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

        if (auction.SellerId != request.UserId)
        {
            return (false, ["Chỉ người bán mới được huỷ phiên này."]);
        }

        // Đã chốt thì không huỷ: quyền sở hữu đã chuyển, huỷ ở đây sẽ tạo trạng thái
        // mâu thuẫn (auction Cancelled nhưng tranh đã thuộc người khác).
        if (auction.Status is AuctionStatus.Settled or AuctionStatus.Cancelled)
        {
            return (false,
                [$"Không huỷ được phiên đang ở trạng thái {AuctionMapper.DescribeStatus(auction.Status)}."]);
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
        auction.IsDeleted = true;

        await _db.SaveChangesAsync(cancellationToken);

        return (true, []);
    }
}
