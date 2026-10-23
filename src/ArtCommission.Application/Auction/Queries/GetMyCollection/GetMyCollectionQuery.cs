using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Queries.GetMyCollection;

public sealed record GetMyCollectionQuery(
    Guid UserId
) : IRequest<(bool Success, IReadOnlyList<MyCollectionItemDto>? Data, string[] Errors)>;

public sealed class GetMyCollectionQueryHandler
    : IRequestHandler<GetMyCollectionQuery, (bool, IReadOnlyList<MyCollectionItemDto>?, string[])>
{
    private readonly IApplicationDbContext _db;

    public GetMyCollectionQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<(bool, IReadOnlyList<MyCollectionItemDto>?, string[])> Handle(
        GetMyCollectionQuery request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return (false, null, ["Yêu cầu đăng nhập để xem bộ sưu tập."]);
        }

        // Lấy tất cả quyền sở hữu hiện tại của người dùng
        var ownerships = await _db.ArtworkOwnerships
            .AsNoTracking()
            .Where(o => o.OwnerId == request.UserId && o.IsCurrent && !o.IsDeleted)
            .OrderByDescending(o => o.AcquiredAt)
            .ToListAsync(cancellationToken);

        if (ownerships.Count == 0)
        {
            return (true, Array.Empty<MyCollectionItemDto>(), []);
        }

        var artworkIds = ownerships.Select(o => o.ArtworkId).Distinct().ToList();
        var auctionIds = ownerships.Where(o => o.AuctionId.HasValue).Select(o => o.AuctionId!.Value).Distinct().ToList();

        var artworks = await _db.Artworks
            .AsNoTracking()
            .Include(a => a.CreatorProfile)
            .Where(a => artworkIds.Contains(a.Id) && !a.IsDeleted)
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var auctions = await _db.Auctions
            .AsNoTracking()
            .Where(a => auctionIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var deliverables = await _db.Deliverables
            .AsNoTracking()
            .Where(d => auctionIds.Contains(d.AuctionId) && !d.IsDeleted)
            .Select(d => d.AuctionId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var deliverableSet = deliverables.ToHashSet();

        var creatorProfileIds = artworks.Values
            .Select(a => a.CreatorProfileId)
            .Distinct()
            .ToList();

        var creatorUsers = await _db.CreatorProfiles
            .AsNoTracking()
            .Include(cp => cp.User)
            .Where(cp => creatorProfileIds.Contains(cp.Id))
            .ToDictionaryAsync(cp => cp.Id, cp => cp.User?.FullName ?? cp.DisplayName ?? "Artist", cancellationToken);

        var result = new List<MyCollectionItemDto>();

        foreach (var o in ownerships)
        {
            if (!artworks.TryGetValue(o.ArtworkId, out var artwork))
            {
                continue;
            }

            auctions.TryGetValue(o.AuctionId ?? Guid.Empty, out var auction);

            var creatorName = creatorUsers.TryGetValue(artwork.CreatorProfileId, out var name)
                ? name
                : (artwork.CreatorProfile?.DisplayName ?? "Dillustration Artist");

            var certNumber = o.AuctionId.HasValue
                ? $"DIL-CERT-{o.AcquiredAt:yyyyMMdd}-{o.AuctionId.Value.ToString()[..8].ToUpperInvariant()}"
                : $"DIL-CERT-OWN-{o.Id.ToString()[..8].ToUpperInvariant()}";

            var hasMaster = o.AuctionId.HasValue && deliverableSet.Contains(o.AuctionId.Value);

            result.Add(new MyCollectionItemDto(
                ArtworkId: artwork.Id,
                Title: artwork.Title,
                ThumbnailUrl: artwork.ThumbnailUrl ?? artwork.WatermarkedUrl ?? artwork.ImageUrl,
                ImageUrl: artwork.WatermarkedUrl ?? artwork.ImageUrl,
                Style: artwork.Style,
                CreatorId: artwork.CreatorProfileId,
                CreatorName: creatorName,
                AcquiredAt: o.AcquiredAt,
                AuctionId: o.AuctionId,
                FinalPrice: auction?.FinalPrice,
                CertificateNumber: certNumber,
                HasMasterFile: hasMaster,
                ExclusiveBadge: "Exclusive 1-of-1 Owner"
            ));
        }

        return (true, result, []);
    }
}
