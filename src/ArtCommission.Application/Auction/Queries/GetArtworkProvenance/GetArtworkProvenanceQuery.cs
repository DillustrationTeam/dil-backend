using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Auction;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Queries.GetArtworkProvenance;

public sealed record GetArtworkProvenanceQuery(
    Guid ArtworkId
) : IRequest<(bool Success, ArtworkProvenanceDto? Data, int StatusCode, string[] Errors)>;

public sealed class GetArtworkProvenanceQueryHandler
    : IRequestHandler<GetArtworkProvenanceQuery, (bool, ArtworkProvenanceDto?, int, string[])>
{
    private readonly IApplicationDbContext _db;

    public GetArtworkProvenanceQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<(bool, ArtworkProvenanceDto?, int, string[])> Handle(
        GetArtworkProvenanceQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ArtworkId == Guid.Empty)
        {
            return (false, null, 400, ["Thiếu mã tác phẩm."]);
        }

        var artwork = await _db.Artworks
            .AsNoTracking()
            .Include(a => a.CreatorProfile)
                .ThenInclude(cp => cp!.User)
            .FirstOrDefaultAsync(a => a.Id == request.ArtworkId && !a.IsDeleted, cancellationToken);

        if (artwork is null)
        {
            return (false, null, 404, ["Không tìm thấy tác phẩm."]);
        }

        var creatorName = artwork.CreatorProfile?.User?.FullName
            ?? artwork.CreatorProfile?.DisplayName
            ?? "Dillustration Artist";

        var ownerships = await _db.ArtworkOwnerships
            .AsNoTracking()
            .Where(o => o.ArtworkId == request.ArtworkId && !o.IsDeleted)
            .OrderBy(o => o.AcquiredAt)
            .ToListAsync(cancellationToken);

        var ownerUserIds = ownerships.Select(o => o.OwnerId).Distinct().ToList();
        var auctionIds = ownerships.Where(o => o.AuctionId.HasValue).Select(o => o.AuctionId!.Value).Distinct().ToList();

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => ownerUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName ?? "Collector", cancellationToken);

        var auctions = await _db.Auctions
            .AsNoTracking()
            .Where(a => auctionIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var history = new List<ArtworkProvenanceHistoryItemDto>();
        ArtworkOwnership? currentOwnership = null;

        foreach (var o in ownerships)
        {
            if (o.IsCurrent)
            {
                currentOwnership = o;
            }

            users.TryGetValue(o.OwnerId, out var ownerName);
            auctions.TryGetValue(o.AuctionId ?? Guid.Empty, out var auction);

            history.Add(new ArtworkProvenanceHistoryItemDto(
                OwnerId: o.OwnerId,
                OwnerName: ownerName ?? "Collector",
                AcquiredAt: o.AcquiredAt,
                ReleasedAt: o.ReleasedAt,
                TransferReason: o.TransferReason.ToString(),
                AuctionId: o.AuctionId,
                Price: auction?.FinalPrice,
                IsCurrent: o.IsCurrent
            ));
        }

        Guid? currentOwnerId = currentOwnership?.OwnerId ?? artwork.CreatorProfile?.UserId;
        string? currentOwnerName = null;
        if (currentOwnerId.HasValue)
        {
            users.TryGetValue(currentOwnerId.Value, out currentOwnerName);
            currentOwnerName ??= (currentOwnerId == artwork.CreatorProfile?.UserId ? creatorName : "Anonymous Collector");
        }

        var isCollector = currentOwnerId.HasValue && currentOwnerId.Value != artwork.CreatorProfile?.UserId;

        var dto = new ArtworkProvenanceDto(
            ArtworkId: artwork.Id,
            ArtworkTitle: artwork.Title,
            ImageUrl: artwork.WatermarkedUrl ?? artwork.ImageUrl,
            CreatorId: artwork.CreatorProfileId,
            CreatorName: creatorName,
            CreatedAt: artwork.CreatedAt,
            CurrentOwnerId: currentOwnerId,
            CurrentOwnerName: currentOwnerName,
            IsCurrentOwnerCollector: isCollector,
            ExclusiveBadge: "Exclusive 1-of-1 Owner",
            History: history
        );

        return (true, dto, 200, []);
    }
}
