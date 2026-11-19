using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Auction.Queries.MyActiveBids;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Queries.EligibleArtworks;

public sealed record EligibleArtworksQuery(Guid UserId, string? Cursor, int Limit)
    : IRequest<(bool Success, IReadOnlyList<EligibleArtworkDto>? Data, object? Meta, string[] Errors)>;

public sealed record EligibleArtworkDto(Guid ArtworkId, string Title, string? ThumbnailUrl, string ImageUrl, string? Style);

public sealed class EligibleArtworksQueryHandler
    : IRequestHandler<EligibleArtworksQuery, (bool, IReadOnlyList<EligibleArtworkDto>?, object?, string[])>
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 100;
    private readonly IApplicationDbContext _db;

    public EligibleArtworksQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<(bool, IReadOnlyList<EligibleArtworkDto>?, object?, string[])> Handle(
        EligibleArtworksQuery request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return (false, null, null, ["Cần đăng nhập để xem tranh đủ điều kiện."]);
        }

        var limit = request.Limit <= 0 ? DefaultLimit : Math.Min(request.Limit, MaxLimit);
        var query = _db.Artworks.AsNoTracking().Where(artwork => !artwork.IsDeleted
            // Tranh phải đã được kiểm duyệt: tranh Pending/Rejected/Hidden không được lên sàn
            // (Marketplace cũng chặn y như vậy). Trước đây thiếu điều kiện này nên tranh vừa
            // upload (Pending) vẫn mở được phiên và hiện công khai ở trang chi tiết phiên.
            && artwork.ModerationStatus == "Approved"
            && (_db.ArtworkOwnerships.Any(o => o.ArtworkId == artwork.Id && o.OwnerId == request.UserId && o.IsCurrent && !o.IsDeleted)
                || (!_db.ArtworkOwnerships.Any(o => o.ArtworkId == artwork.Id && o.IsCurrent && !o.IsDeleted)
                    && artwork.CreatorProfile != null
                    && !artwork.CreatorProfile.IsDeleted
                    && artwork.CreatorProfile.UserId == request.UserId))
            && !_db.Auctions.Any(auction => auction.ArtworkId == artwork.Id && !auction.IsDeleted
                && (auction.Status == AuctionStatus.Scheduled
                    || auction.Status == AuctionStatus.Active
                    || (auction.Status == AuctionStatus.Ended
                        && (auction.BidCount > 0 || auction.WinnerId != null))
                    // Các trạng thái SAU khi chốt vẫn đang giữ tranh: ký quỹ chưa đóng, hàng có
                    // thể đang trên đường, quyền sở hữu đã sang người thắng. Thiếu các trạng
                    // thái này thì người thắng thấy tranh "đủ điều kiện" và niêm yết lại một
                    // bức tranh vật lý chưa giao xong.
                    || auction.Status == AuctionStatus.AwaitingPhysical
                    || auction.Status == AuctionStatus.InTransit
                    || auction.Status == AuctionStatus.Disputed
                    || auction.Status == AuctionStatus.Completed)));

        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            if (!AuctionListCursor.TryDecode(request.Cursor, out var createdAt, out var artworkId))
            {
                return (false, null, null, ["Cursor không hợp lệ."]);
            }

            query = query.Where(a => a.CreatedAt < createdAt
                || (a.CreatedAt == createdAt && a.Id.CompareTo(artworkId) > 0));
        }

        var rows = await query.OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id)
            .Take(limit + 1)
            .Select(a => new { a.Id, a.CreatedAt, a.Title, a.ThumbnailUrl, a.ImageUrl, a.Style })
            .ToListAsync(cancellationToken);
        var hasMore = rows.Count > limit;
        var page = hasMore ? rows.Take(limit).ToList() : rows;
        var data = page.Select(a => new EligibleArtworkDto(a.Id, a.Title, a.ThumbnailUrl, a.ImageUrl, a.Style)).ToList();
        var last = page.LastOrDefault();
        var nextCursor = hasMore && last is not null ? AuctionListCursor.Encode(last.CreatedAt, last.Id) : null;
        return (true, data, new { nextCursor, count = data.Count }, []);
    }
}
