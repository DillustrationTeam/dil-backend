using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Commands.UnwatchAuction;

/// <summary>
/// UC32 — DELETE /api/v1/auctions/{auctionId}/watch
/// Bỏ theo dõi phiên. Idempotent: bỏ theo dõi khi chưa từng theo dõi vẫn trả thành công,
/// vì kết quả người dùng mong muốn ("tôi không theo dõi phiên này") đã đúng.
/// </summary>
public record UnwatchAuctionCommand(
    Guid UserId,
    Guid AuctionId
) : IRequest<(bool Success, AuctionWatchDto? Data, string[] Errors)>;

public class UnwatchAuctionCommandHandler
    : IRequestHandler<UnwatchAuctionCommand, (bool, AuctionWatchDto?, string[])>
{
    private readonly IApplicationDbContext _db;

    public UnwatchAuctionCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, AuctionWatchDto?, string[])> Handle(
        UnwatchAuctionCommand request,
        CancellationToken cancellationToken)
    {
        if (request.AuctionId == Guid.Empty)
        {
            return (false, null, ["Thiếu mã phiên đấu giá."]);
        }

        var watch = await _db.AuctionWatches.FirstOrDefaultAsync(
            w => w.AuctionId == request.AuctionId && w.UserId == request.UserId,
            cancellationToken);

        if (watch is not null && !watch.IsDeleted)
        {
            // Giữ dòng lại nhưng đánh dấu đã bỏ: nếu xoá cứng thì lần theo dõi sau
            // sẽ đụng unique index (AuctionId, UserId).
            watch.IsDeleted = true;
            watch.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return (true, new AuctionWatchDto(watch?.Id, request.AuctionId, request.UserId, false), []);
    }
}
