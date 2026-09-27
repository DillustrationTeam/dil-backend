using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Queries.GetAuctionById;

/// <summary>
/// UC32 — GET /api/v1/auctions/{auctionId}
/// Chi tiết phiên: giá hiện tại, số lượt bid, thời gian còn lại, lịch sử bid gần nhất.
///
/// LƯU Ý BẢO MẬT: <c>reservePrice</c> là giá sàn BÍ MẬT. Không trả cho người ngoài
/// (không phải seller và không phải người đang dẫn đầu), nếu không thì giá sàn
/// trở thành thông tin công khai và mất hết tác dụng.
/// </summary>
public record GetAuctionByIdQuery(
    Guid AuctionId,
    Guid? CurrentUserId,
    int RecentBidCount = 20
) : IRequest<(bool Success, AuctionDetailDto? Data, IReadOnlyList<BidDto>? RecentBids, string[] Errors)>;

public class GetAuctionByIdQueryHandler
    : IRequestHandler<GetAuctionByIdQuery, (bool, AuctionDetailDto?, IReadOnlyList<BidDto>?, string[])>
{
    private const int MaxRecentBids = 50;

    private readonly IApplicationDbContext _db;

    public GetAuctionByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, AuctionDetailDto?, IReadOnlyList<BidDto>?, string[])> Handle(
        GetAuctionByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (request.AuctionId == Guid.Empty)
        {
            return (false, null, null, ["Thiếu mã phiên đấu giá."]);
        }

        var auction = await _db.Auctions
            .AsNoTracking()
            .Include(a => a.Artwork)
            .FirstOrDefaultAsync(a => a.Id == request.AuctionId && !a.IsDeleted, cancellationToken);

        if (auction is null)
        {
            return (false, null, null, ["Không tìm thấy phiên đấu giá."]);
        }

        var seller = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == auction.SellerId)
            .Select(u => new { u.Id, u.FullName })
            .FirstOrDefaultAsync(cancellationToken);

        var watchCount = await _db.AuctionWatches
            .AsNoTracking()
            .CountAsync(w => w.AuctionId == auction.Id && !w.IsDeleted, cancellationToken);

        var isWatching = request.CurrentUserId.HasValue
                         && await _db.AuctionWatches
                             .AsNoTracking()
                             .AnyAsync(
                                 w => w.AuctionId == auction.Id
                                      && w.UserId == request.CurrentUserId.Value
                                      && !w.IsDeleted,
                                 cancellationToken);

        var leading = await _db.Bids
            .AsNoTracking()
            .Where(b => b.AuctionId == auction.Id && b.Status == BidStatus.Leading && !b.IsDeleted)
            .Select(b => new { b.BidderId })
            .FirstOrDefaultAsync(cancellationToken);

        // Chỉ seller và người đang dẫn đầu được thấy giá sàn.
        var canSeeReserve = request.CurrentUserId.HasValue
                            && (request.CurrentUserId.Value == auction.SellerId
                                || (leading is not null && leading.BidderId == request.CurrentUserId.Value));

        var recentCount = request.RecentBidCount <= 0
            ? 20
            : Math.Min(request.RecentBidCount, MaxRecentBids);

        // Lấy bid thô rồi nạp tên người đặt trong MỘT truy vấn — tránh subquery
        // tương quan theo từng dòng bid (N+1).
        var rawBids = await _db.Bids
            .AsNoTracking()
            .Where(b => b.AuctionId == auction.Id && !b.IsDeleted)
            .OrderByDescending(b => b.PlacedAt)
            .Take(recentCount)
            .ToListAsync(cancellationToken);

        var bidderIds = rawBids.Select(b => b.BidderId).Distinct().ToList();

        var bidderNames = await _db.Users
            .AsNoTracking()
            .Where(u => bidderIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var recentBids = rawBids
            .Select(b => AuctionMapper.ToDto(
                b,
                bidderNames.TryGetValue(b.BidderId, out var name) ? name : null))
            .ToList();

        var detail = new AuctionDetailDto(
            AuctionId: auction.Id,
            Artwork: AuctionMapper.ToArtworkDto(auction.Artwork),
            Seller: seller is null ? null : new AuctionSellerDto(seller.Id, seller.FullName),
            AuctionType: auction.AuctionType.ToString(),
            StartPrice: auction.StartPrice,
            ReservePrice: canSeeReserve ? auction.ReservePrice : null,
            BuyNowPrice: auction.BuyNowPrice,
            BidStep: auction.BidStep,
            CurrentPrice: auction.CurrentPrice,
            BidCount: auction.BidCount,
            WatchCount: watchCount,
            WinnerId: auction.WinnerId,
            StartAt: auction.StartAt,
            EndAt: auction.EndAt,
            AuctionStatus: auction.Status.ToString(),
            PaymentDeadline: auction.PaymentDeadline,
            IsWatching: isWatching,
            LeadingBidderId: leading?.BidderId);

        return (true, detail, recentBids, []);
    }
}
