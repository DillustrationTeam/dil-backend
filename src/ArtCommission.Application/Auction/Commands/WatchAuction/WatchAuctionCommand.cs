using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Commands.WatchAuction;

/// <summary>
/// UC32 — POST /api/v1/auctions/{auctionId}/watch
/// Theo dõi phiên để nhận cảnh báo bị đè giá / sắp kết thúc.
///
/// IDEMPOTENT có chủ đích: bấm theo dõi lần thứ hai trả về đúng dòng đang có,
/// KHÔNG tạo dòng thứ hai và KHÔNG báo lỗi. Nút trên UI có thể bị double-click,
/// và lỗi "đã theo dõi rồi" là lỗi vô nghĩa với người dùng.
/// </summary>
public record WatchAuctionCommand(
    Guid UserId,
    Guid AuctionId
) : IRequest<(bool Success, AuctionWatchDto? Data, string[] Errors)>;

public class WatchAuctionCommandHandler
    : IRequestHandler<WatchAuctionCommand, (bool, AuctionWatchDto?, string[])>
{
    private readonly IApplicationDbContext _db;

    public WatchAuctionCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, AuctionWatchDto?, string[])> Handle(
        WatchAuctionCommand request,
        CancellationToken cancellationToken)
    {
        if (request.AuctionId == Guid.Empty)
        {
            return (false, null, ["Thiếu mã phiên đấu giá."]);
        }

        var auction = await _db.Auctions
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AuctionId && !a.IsDeleted, cancellationToken);

        if (auction is null)
        {
            return (false, null, ["Không tìm thấy phiên đấu giá."]);
        }

        // Theo dõi phiên đã chốt/đã huỷ là vô nghĩa và làm rác bảng watch.
        if (auction.Status is AuctionStatus.Settled or AuctionStatus.Cancelled or AuctionStatus.Expired)
        {
            return (false, null, ["Phiên này đã kết thúc nên không theo dõi được."]);
        }

        var existing = await _db.AuctionWatches.FirstOrDefaultAsync(
            w => w.AuctionId == request.AuctionId && w.UserId == request.UserId,
            cancellationToken);

        if (existing is not null)
        {
            // Có thể đã bị bỏ theo dõi trước đó — bật lại thay vì báo lỗi.
            if (existing.IsDeleted)
            {
                existing.IsDeleted = false;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
            }

            return (true, new AuctionWatchDto(existing.Id, existing.AuctionId, existing.UserId, true), []);
        }

        var watch = new AuctionWatch
        {
            AuctionId = request.AuctionId,
            UserId = request.UserId
        };

        _db.AuctionWatches.Add(watch);
        await _db.SaveChangesAsync(cancellationToken);

        return (true, new AuctionWatchDto(watch.Id, watch.AuctionId, watch.UserId, true), []);
    }
}
