using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Queries.GetAuctionById;

/// <summary>
/// UC32 — GET /api/v1/auctions/{auctionId}
/// Chi tiết phiên: giá hiện tại, số lượt bid, thời gian còn lại, lịch sử bid gần nhất.
///
/// (không phải seller và không phải người đang dẫn đầu), nếu không thì giá sàn
/// trở thành thông tin công khai và mất hết tác dụng.
/// </summary>
public record GetAuctionByIdQuery(
    Guid AuctionId,
    Guid? CurrentUserId,
    int RecentBidCount = 20,
    /// <summary>
    /// Người gọi là quản trị viên. Endpoint chi tiết phiên mở cho cả khách chưa đăng nhập,
    /// nên cờ này quyết định có trả khối giao nhận/biên lai (dữ liệu riêng) hay không.
    /// </summary>
    bool IsAdmin = false
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
            // Hủy phiên dùng xóa mềm, nhưng chi tiết Cancelled vẫn cần tra cứu được.
            .FirstOrDefaultAsync(a => a.Id == request.AuctionId
                                     && (!a.IsDeleted || a.Status == AuctionStatus.Cancelled), cancellationToken);

        if (auction is null)
        {
            return (false, null, null, ["Không tìm thấy phiên đấu giá."]);
        }

        var seller = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == auction.SellerId)
            .Select(u => new { u.Id, u.FullName })
            .FirstOrDefaultAsync(cancellationToken);

        // Nút "Xem profile" cần mã HỒ SƠ CREATOR (không phải userId), nếu không trang
        // profile công khai sẽ tra cứu sai khoá và trả 404.
        var sellerCreatorProfileId = await _db.CreatorProfiles
            .AsNoTracking()
            .Where(p => p.UserId == auction.SellerId && !p.IsDeleted)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var watchCount = await _db.AuctionWatches
            .AsNoTracking()
            .CountAsync(w => w.AuctionId == auction.Id && !w.IsDeleted, cancellationToken);

        var deliverableCount = await _db.Deliverables
            .AsNoTracking()
            .CountAsync(d => d.AuctionId == auction.Id && !d.IsDeleted, cancellationToken);

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

        var topBidderRows = await _db.Bids
            .AsNoTracking()
            .Where(b => b.AuctionId == auction.Id && !b.IsDeleted)
            .GroupBy(b => b.BidderId)
            .Select(group => new
            {
                UserId = group.Key,
                HighestBid = group.Max(b => b.Amount),
                BidCount = group.Count()
            })
            .OrderByDescending(row => row.HighestBid)
            .Take(7)
            .ToListAsync(cancellationToken);

        var participantIds = topBidderRows.Select(row => row.UserId).ToList();
        var participantNames = await _db.Users
            .AsNoTracking()
            .Where(user => participantIds.Contains(user.Id))
            .Select(user => new { user.Id, user.FullName })
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

        var participants = new List<AuctionParticipantDto>();
        if (seller is not null)
        {
            participants.Add(new AuctionParticipantDto(seller.Id, seller.FullName, "Seller", null, 0));
        }
        participants.AddRange(topBidderRows.Select(row => new AuctionParticipantDto(
            row.UserId,
            participantNames.GetValueOrDefault(row.UserId),
            "Bidder",
            row.HighestBid,
            row.BidCount)));

        var recentBids = rawBids
            .Select(b => AuctionMapper.ToDto(
                b,
                bidderNames.TryGetValue(b.BidderId, out var name) ? name : null))
            .ToList();

        var delivery = await _db.Set<AuctionDelivery>()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.AuctionId == auction.Id && !d.IsDeleted, cancellationToken);

        var escrow = await _db.EscrowTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.AuctionId == auction.Id && !e.IsDeleted, cancellationToken);

        // Quyền đánh giá người bán: chỉ người thắng, sau khi giao dịch khép lại, và mỗi phiên 1 lần.
        var hasRatedSeller = request.CurrentUserId is { } viewerId
                              && viewerId != Guid.Empty
                              && await _db.CreatorReviews
                                  .AsNoTracking()
                                  .AnyAsync(r => r.AuctionId == auction.Id
                                                 && r.ReviewerUserId == viewerId
                                                 && !r.IsDeleted,
                                      cancellationToken);

        var transactionClosed =
            auction.Status == AuctionStatus.Completed
            || delivery is { Status: DeliveryStatus.Delivered or DeliveryStatus.Resolved }
            || delivery?.DisputeResolvedAt is not null;

        var canRateSeller = transactionClosed
                            && !hasRatedSeller
                            && request.CurrentUserId == auction.WinnerId;

        // Mức phạt hiện tại của người bán: lấy bậc cao nhất trong các án phạt chưa bị thu hồi.
        // Thang phạt cộng dồn tới mức 5 nên đây là mức người bán đang đứng, không phải mức của phiên này.
        // CHỈ trả cho chính người bán hoặc quản trị viên — đây là dữ liệu uy tín nội bộ của
        // người bán, không phải thông tin công khai của phiên.
        var canSeeSellerPenalty = request.IsAdmin || request.CurrentUserId == auction.SellerId;
        var sellerPenaltyLevel = !canSeeSellerPenalty
            ? 0
            : await _db.AuctionPenalties
                .AsNoTracking()
                .Where(p => p.SellerId == auction.SellerId
                            && !p.IsDeleted
                            && p.Status != PenaltyStatus.Revoked)
                .Select(p => (int?)p.StrikeLevel)
                .MaxAsync(cancellationToken) ?? 0;

        // Trần auto-bid của chính người xem: cần để banner "Auto-bid đang hoạt động" sống
        // qua lần tải lại trang (trước đây chỉ nằm trong RAM của tab đang mở).
        decimal? myAutoBidMax = null;
        if (request.CurrentUserId is { } autoBidViewer && autoBidViewer != Guid.Empty)
        {
            myAutoBidMax = await _db.AuctionAutoBids
                .AsNoTracking()
                .Where(x => x.AuctionId == auction.Id && x.BidderId == autoBidViewer && !x.IsDeleted)
                .Select(x => (decimal?)x.MaxAmount)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // KHỐI DỮ LIỆU RIÊNG: đơn giao nhận (tên, số điện thoại, địa chỉ người nhận, mã vận
        // đơn) và biên lai (số tiền ký quỹ, người trả/người nhận tiền). Endpoint chi tiết
        // phiên mở cho cả khách chưa đăng nhập, nên chỉ người trong cuộc hoặc quản trị viên
        // mới được nhận; người ngoài nhận null thay vì dữ liệu của người khác.
        var canSeePrivateFulfilment =
            request.IsAdmin
            || (request.CurrentUserId is { } viewer && viewer != Guid.Empty
                && (viewer == auction.SellerId || viewer == auction.WinnerId));

        var detail = new AuctionDetailDto(
            AuctionId: auction.Id,
            Artwork: AuctionMapper.ToArtworkDto(auction.Artwork) is { } artwork
                ? artwork with { DeliverableCount = deliverableCount, HasMasterFilePack = deliverableCount > 0 }
                : null,
            Seller: seller is null ? null : new AuctionSellerDto(seller.Id, seller.FullName, sellerCreatorProfileId),
            AuctionType: auction.AuctionType.ToString(),
            StartPrice: auction.StartPrice,
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
            LeadingBidderId: leading?.BidderId,
            Participants: participants,
            HasPhysicalCopy: auction.HasPhysicalCopy,
            PhysicalDescription: auction.PhysicalDescription,
            RequiresPhysicalDelivery: auction.RequiresPhysicalDelivery,
            PhysicalChoiceDeadline: auction.PhysicalChoiceDeadline,
            Delivery: canSeePrivateFulfilment
                ? AuctionMapper.ToDeliveryDto(delivery, canRateSeller, hasRatedSeller)
                : null,
            Receipts: canSeePrivateFulfilment
                ? AuctionMapper.BuildReceipts(auction, escrow, delivery)
                : null,
            FinalPrice: auction.FinalPrice,
            SellerPenaltyLevel: sellerPenaltyLevel,
            MyAutoBidMax: myAutoBidMax);
        return (true, detail, recentBids, []);
    }
}
