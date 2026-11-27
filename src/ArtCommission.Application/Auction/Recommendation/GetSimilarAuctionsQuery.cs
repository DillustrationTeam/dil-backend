using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Recommendation;

/// <summary>
/// "Auctions similar to this one" for the detail page: same tags/styles first, then the same
/// creator, then the same category. No profile needed, so it works for anonymous visitors too.
/// </summary>
public sealed record GetSimilarAuctionsQuery(Guid AuctionId, int Take = 6)
    : IRequest<(bool Success, IReadOnlyList<RecommendedAuctionDto> Data, string[] Errors)>;

public sealed class GetSimilarAuctionsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetSimilarAuctionsQuery, (bool Success, IReadOnlyList<RecommendedAuctionDto> Data, string[] Errors)>
{
    public async Task<(bool, IReadOnlyList<RecommendedAuctionDto>, string[])> Handle(
        GetSimilarAuctionsQuery request,
        CancellationToken cancellationToken)
    {
        var take = Math.Clamp(request.Take, 2, 12);
        var now = DateTimeOffset.UtcNow;

        var source = await db.Auctions.AsNoTracking()
            .Where(a => a.Id == request.AuctionId && !a.IsDeleted)
            .Select(a => new
            {
                a.Id,
                a.ArtworkId,
                CreatorProfileId = a.Artwork!.CreatorProfileId,
                Style = a.Artwork!.Style,
                CategoryId = a.Artwork!.CategoryId,
                Tags = a.Artwork!.ArtworkTags.Where(t => t.Tag != null).Select(t => t.Tag!.Name).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (source is null)
        {
            return (false, [], ["KhÃ´ng tÃ¬m tháº¥y phiÃªn Ä‘áº¥u giÃ¡."]);
        }

        var tags = source.Tags;

        var rows = await db.Auctions.AsNoTracking()
            .Where(a => !a.IsDeleted
                        && a.Id != source.Id
                        && (a.Status == AuctionStatus.Active || a.Status == AuctionStatus.Scheduled)
                        && a.EndAt > now
                        && (a.Artwork!.CreatorProfileId == source.CreatorProfileId
                            || a.Artwork!.CategoryId == source.CategoryId
                            || a.Artwork!.ArtworkTags.Any(t => t.Tag != null && tags.Contains(t.Tag!.Name))))
            .OrderBy(a => a.EndAt)
            .Take(60)
            .Select(a => new
            {
                a.Id,
                a.ArtworkId,
                Title = a.Artwork!.Title,
                ThumbnailUrl = a.Artwork!.ThumbnailUrl ?? a.Artwork!.WatermarkedUrl ?? a.Artwork!.ImageUrl,
                ImageUrl = a.Artwork!.WatermarkedUrl ?? a.Artwork!.ImageUrl,
                Style = a.Artwork!.Style,
                CreatorProfileId = a.Artwork!.CreatorProfileId,
                CategoryId = a.Artwork!.CategoryId,
                AuctionTags = a.Artwork!.ArtworkTags.Where(t => t.Tag != null).Select(t => t.Tag!.Name).ToList(),
                a.CurrentPrice,
                a.BidCount,
                WatchCount = a.Watches.Count(w => !w.IsDeleted),
                a.StartAt,
                a.EndAt,
                a.Status,
                a.HasPhysicalCopy,
                a.PhysicalDescription,
                a.FinalPrice,
                a.RiskLevel
            })
            .ToListAsync(cancellationToken);

        var scored = rows
            .Select(r =>
            {
                var sharedTags = r.AuctionTags.Intersect(tags, StringComparer.OrdinalIgnoreCase).ToList();
                var score =
                    (sharedTags.Count * 3d)
                    + (r.CreatorProfileId == source.CreatorProfileId ? 2d : 0d)
                    + (r.CategoryId == source.CategoryId ? 1d : 0d)
                    + (r.Style == source.Style ? 0.5d : 0d)
                    - (r.RiskLevel is AuctionRiskLevel.High or AuctionRiskLevel.Critical ? 3d : 0d);

                var reason = sharedTags.Count > 0
                    ? $"CÃ¹ng phong cÃ¡ch {sharedTags[0]}"
                    : r.CreatorProfileId == source.CreatorProfileId
                        ? "CÃ¹ng creator"
                        : "CÃ¹ng danh má»¥c";

                return new
                {
                    Row = r,
                    Score = score,
                    Reason = reason,
                    Signals = sharedTags.Select(t => $"tag:{t}").ToList()
                };
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Row.EndAt)
            .Take(take)
            .ToList();

        var items = scored
            .Select(x => new RecommendedAuctionDto(
                new AuctionListItemDto(
                    AuctionId: x.Row.Id,
                    Artwork: new AuctionArtworkDto(
                        x.Row.ArtworkId,
                        x.Row.Title,
                        x.Row.ThumbnailUrl,
                        x.Row.ImageUrl,
                        x.Row.Style),
                    CurrentPrice: x.Row.CurrentPrice,
                    BidCount: x.Row.BidCount,
                    StartAt: x.Row.StartAt,
                    EndAt: x.Row.EndAt,
                    AuctionStatus: x.Row.Status.ToString(),
                    WatchCount: x.Row.WatchCount,
                    HasPhysicalCopy: x.Row.HasPhysicalCopy,
                    PhysicalDescription: x.Row.PhysicalDescription,
                    FinalPrice: x.Row.FinalPrice),
                decimal.Round((decimal)x.Score, 2),
                x.Reason,
                x.Signals))
            .ToList();

        return (true, items, []);
    }
}

// =====================================================================
// Record impressions (CTR measurement + repeat suppression)
// =====================================================================
public sealed record RecordRecommendationImpressionsCommand(
    Guid? UserId,
    IReadOnlyList<RecommendationImpressionItem> Items,
    string? Surface,
    bool Clicked)
    : IRequest<(bool Success, int Recorded, string[] Errors)>;

public sealed class RecordRecommendationImpressionsCommandHandler(IApplicationDbContext db)
    : IRequestHandler<RecordRecommendationImpressionsCommand, (bool Success, int Recorded, string[] Errors)>
{
    private const int MaxBatch = 30;

    public async Task<(bool, int, string[])> Handle(
        RecordRecommendationImpressionsCommand request,
        CancellationToken cancellationToken)
    {
        var items = (request.Items ?? [])
            .Where(item => item.AuctionId != Guid.Empty)
            .GroupBy(item => item.AuctionId)
            .Select(group => group.First())
            .ToList();

        if (items.Count == 0)
        {
            return (false, 0, ["Danh sách impression rỗng."]);
        }

        if (items.Count > MaxBatch)
        {
            return (false, 0, [$"Mỗi lượt chỉ ghi tối đa {MaxBatch} impression."]);
        }

        var surface = string.IsNullOrWhiteSpace(request.Surface) ? "auction-list" : request.Surface.Trim();
        if (surface.Length > 32)
        {
            surface = surface[..32];
        }

        // Unknown ids are dropped, never fatal: impressions are telemetry, not a transaction.
        var ids = items.Select(item => item.AuctionId).ToList();
        var known = await db.Auctions.AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        if (known.Count == 0)
        {
            return (true, 0, []);
        }

        var now = DateTimeOffset.UtcNow;
        var userId = request.UserId ?? Guid.Empty;

        foreach (var item in items.Where(item => known.Contains(item.AuctionId)))
        {
            db.RecommendationImpressions.Add(new Domain.Entities.Auction.RecommendationImpression
            {
                UserId = userId,
                AuctionId = item.AuctionId,
                Surface = surface,
                // Score/rank are what the client actually displayed, so CTR can be joined with the
                // ranking position later instead of being an opaque counter.
                Score = item.Score ?? 0m,
                Rank = item.Rank ?? 0,
                Reason = string.Empty,
                ShownAt = now,
                ClickedAt = request.Clicked ? now : null,
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return (true, known.Count, []);
    }
}