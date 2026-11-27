using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Recommendation;

public sealed record GetRecommendedAuctionsQuery(Guid? UserId, int Take = 12)
    : IRequest<(bool Success, IReadOnlyList<RecommendedAuctionDto> Data, string[] Errors)>;

public sealed class GetRecommendedAuctionsQueryHandler(
    IApplicationDbContext db,
    IInterestProfileBuilder profileBuilder)
    : IRequestHandler<GetRecommendedAuctionsQuery, (bool Success, IReadOnlyList<RecommendedAuctionDto> Data, string[] Errors)>
{
    public async Task<(bool, IReadOnlyList<RecommendedAuctionDto>, string[])> Handle(
        GetRecommendedAuctionsQuery request,
        CancellationToken cancellationToken)
    {
        var take = Math.Clamp(request.Take, 3, 30);
        var now = DateTimeOffset.UtcNow;

        // Weights come from PlatformConfigs (fallback = shipped defaults) so tuning needs no deploy.
        var weights = await RecommendationWeightsConfig.LoadAsync(db, cancellationToken);

        var profile = request.UserId is { } userId && userId != Guid.Empty
            ? await profileBuilder.GetOrBuildAsync(userId, forceRebuild: false, cancellationToken)
            : InterestProfile.Empty;

        var candidates = await LoadCandidatesAsync(request.UserId, now, weights, cancellationToken);
        if (candidates.Count == 0)
        {
            return (true, [], []);
        }

        var scored = Rank(candidates, profile, now, weights);

        // Diversity pass: no single creator or category may dominate the rail.
        var picked = new List<ScoredCandidate>();
        var perCreator = new Dictionary<Guid, int>();
        var perCategory = new Dictionary<string, int>();

        foreach (var candidate in scored)
        {
            if (perCreator.GetValueOrDefault(candidate.CreatorProfileId) >= weights.MaxPerCreator)
            {
                continue;
            }

            var categoryKey = candidate.CategoryName ?? "khac";
            if (perCategory.GetValueOrDefault(categoryKey) >= weights.MaxPerCategory)
            {
                continue;
            }

            perCreator[candidate.CreatorProfileId] = perCreator.GetValueOrDefault(candidate.CreatorProfileId) + 1;
            perCategory[categoryKey] = perCategory.GetValueOrDefault(categoryKey) + 1;
            picked.Add(candidate);

            if (picked.Count == take)
            {
                break;
            }
        }

        // If the diversity filter starved the rail, top it up ignoring the caps rather than
        // returning an almost-empty section.
        if (picked.Count < Math.Min(take, 3))
        {
            foreach (var candidate in scored.Where(c => !picked.Contains(c)))
            {
                picked.Add(candidate);
                if (picked.Count == take)
                {
                    break;
                }
            }
        }

        var result = picked
            .Select(c => new RecommendedAuctionDto(
                c.ToListItemDto(),
                decimal.Round((decimal)c.Score * 100m, 1),
                c.Reason,
                c.Signals))
            .ToList();

        return (true, result, []);
    }

    private static List<ScoredCandidate> Rank(
        IReadOnlyList<CandidateRow> candidates,
        InterestProfile profile,
        DateTimeOffset now,
        RecommendationWeightsConfig weights)
    {
        var contentScores = candidates
            .Select(c => profile.ContentScore(c.Tags, c.Style, c.CategoryName))
            .ToList();
        var maxContent = contentScores.Count == 0 ? 0m : contentScores.Max();

        var maxBids = Math.Max(1, candidates.Max(c => c.BidCount));
        var maxWatches = Math.Max(1, candidates.Max(c => c.WatchCount));

        var scored = new List<ScoredCandidate>(candidates.Count);

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var signals = new List<string>();

            var content = maxContent > 0 ? (double)(contentScores[i] / maxContent) : 0d;
            if (contentScores[i] > 0)
            {
                var bestTag = profile.BestTag(candidate.Tags);
                signals.Add(bestTag is null ? "content" : $"tag:{bestTag}");
            }

            var hoursLeft = Math.Max(0d, (candidate.EndAt - now).TotalHours);
            var urgency = Math.Clamp(1d - (hoursLeft / (weights.WindowDays * 24d)), 0d, 1d);
            if (hoursLeft <= 24)
            {
                signals.Add("ending-soon");
            }

            var popularity =
                (Math.Log(1d + candidate.BidCount) / Math.Log(1d + 50d) * 0.7)
                + (Math.Log(1d + candidate.WatchCount) / Math.Log(1d + 100d) * 0.3);

            var priceFit = ComputePriceFit(candidate.CurrentPrice, profile.MedianBidAmount);
            if (priceFit > 0.7)
            {
                signals.Add("price-fit");
            }

            var creatorAffinity = profile.CreatorAffinity(candidate.CreatorProfileId);
            var creator = creatorAffinity > 0 ? Math.Clamp((double)(creatorAffinity / 12m), 0d, 1d) : 0d;
            if (creatorAffinity > 0)
            {
                signals.Add("creator");
            }

            var riskPenalty = candidate.RiskLevel is AuctionRiskLevel.High or AuctionRiskLevel.Critical
                ? weights.RiskPenalty
                : 0d;
            if (riskPenalty > 0)
            {
                signals.Add("risk-downranked");
            }

            var score = (content * weights.Content)
                        + (urgency * weights.Urgency)
                        + (Math.Clamp(popularity, 0d, 1d) * weights.Popularity)
                        + (priceFit * weights.PriceFit)
                        + (creator * weights.Creator)
                        - riskPenalty;

            scored.Add(new ScoredCandidate(
                candidate,
                score,
                BuildReason(candidate, profile, hoursLeft, creatorAffinity > 0, signals),
                signals));
        }

        return scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Candidate.EndAt)
            .ThenBy(s => s.Candidate.AuctionId)
            .ToList();
    }

    /// <summary>
    /// 1.0 when the auction sits in the price band the user actually bids in, decaying with distance
    /// on a log scale so a 10x jump is not treated as "close enough".
    /// </summary>
    private static double ComputePriceFit(decimal price, decimal medianBid)
    {
        if (medianBid <= 0 || price <= 0)
        {
            return 0.5d;
        }

        var ratio = (double)(price / medianBid);
        if (ratio <= 0)
        {
            return 0d;
        }

        var distance = Math.Abs(Math.Log10(ratio));
        return Math.Clamp(1d - (distance / 1d), 0d, 1d);
    }

    private static string BuildReason(
        CandidateRow candidate,
        InterestProfile profile,
        double hoursLeft,
        bool followsCreator,
        IReadOnlyList<string> signals)
    {
        var bestTag = profile.BestTag(candidate.Tags);
        if (bestTag is not null)
        {
            return $"Vì bạn quan tâm {bestTag}";
        }

        if (followsCreator)
        {
            return "Từ creator bạn theo dõi";
        }

        if (hoursLeft <= 24)
        {
            return hoursLeft <= 1
                ? "Sắp kết thúc trong chưa đầy 1 giờ"
                : $"Sắp kết thúc trong {Math.Ceiling(hoursLeft)} giờ";
        }

        if (signals.Contains("price-fit"))
        {
            return "Cùng tầm giá bạn thường đấu";
        }

        return profile.IsEmpty ? "Đang được chú ý" : "Có thể bạn quan tâm";
    }

    private async Task<List<CandidateRow>> LoadCandidatesAsync(
        Guid? userId,
        DateTimeOffset now,
        RecommendationWeightsConfig weights,
        CancellationToken cancellationToken)
    {
        var horizon = now.AddDays(weights.WindowDays);

        // The window is an ORDERING preference, not a filter: a marketplace without auctions closing
        // this week would otherwise render an empty rail. Urgency simply saturates at 0 past the
        // horizon, and the popularity/personalisation terms keep ordering sane.
        var query = db.Auctions.AsNoTracking()
            .Where(a => !a.IsDeleted
                        && (a.Status == AuctionStatus.Active || a.Status == AuctionStatus.Scheduled)
                        && a.EndAt > now);

        if (userId is { } currentUser && currentUser != Guid.Empty)
        {
            // Never recommend what the user is already selling, bidding on, or watching.
            var bidAuctionIds = await db.Bids.AsNoTracking()
                .Where(b => b.BidderId == currentUser && !b.IsDeleted)
                .Select(b => b.AuctionId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var watchedIds = await db.AuctionWatches.AsNoTracking()
                .Where(w => w.UserId == currentUser && !w.IsDeleted)
                .Select(w => w.AuctionId)
                .ToListAsync(cancellationToken);

            var excluded = bidAuctionIds.Concat(watchedIds).ToHashSet();
            query = query.Where(a => a.SellerId != currentUser
                                     && !excluded.Contains(a.Id));
        }

        var rows = await query
            .OrderBy(a => a.EndAt > horizon ? 1 : 0)
            .ThenBy(a => a.EndAt)
            .Take(300)
            .Select(a => new CandidateRow
            {
                AuctionId = a.Id,
                ArtworkId = a.ArtworkId,
                Title = a.Artwork!.Title,
                ThumbnailUrl = a.Artwork!.ThumbnailUrl ?? a.Artwork!.WatermarkedUrl ?? a.Artwork!.ImageUrl,
                ImageUrl = a.Artwork!.WatermarkedUrl ?? a.Artwork!.ImageUrl,
                Style = a.Artwork!.Style,
                CategoryName = a.Artwork!.Category != null ? a.Artwork!.Category!.Name : null,
                CreatorProfileId = a.Artwork!.CreatorProfileId,
                Tags = a.Artwork!.ArtworkTags.Where(t => t.Tag != null).Select(t => t.Tag!.Name).ToList(),
                CurrentPrice = a.CurrentPrice,
                BidCount = a.BidCount,
                WatchCount = a.Watches.Count(w => !w.IsDeleted),
                StartAt = a.StartAt,
                EndAt = a.EndAt,
                Status = a.Status,
                HasPhysicalCopy = a.HasPhysicalCopy,
                PhysicalDescription = a.PhysicalDescription,
                FinalPrice = a.FinalPrice,
                RiskLevel = a.RiskLevel
            })
            .ToListAsync(cancellationToken);

        // Union with the most-watched auctions so a quiet week still has something to show.
        var popular = await query
            .OrderByDescending(a => a.Watches.Count(w => !w.IsDeleted))
            .ThenByDescending(a => a.BidCount)
            .Take(100)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        var missing = popular.Where(id => rows.All(r => r.AuctionId != id)).ToList();
        if (missing.Count > 0)
        {
            var extra = await db.Auctions.AsNoTracking()
                .Where(a => missing.Contains(a.Id))
                .Select(a => new CandidateRow
                {
                    AuctionId = a.Id,
                    ArtworkId = a.ArtworkId,
                    Title = a.Artwork!.Title,
                    ThumbnailUrl = a.Artwork!.ThumbnailUrl ?? a.Artwork!.WatermarkedUrl ?? a.Artwork!.ImageUrl,
                    ImageUrl = a.Artwork!.WatermarkedUrl ?? a.Artwork!.ImageUrl,
                    Style = a.Artwork!.Style,
                    CategoryName = a.Artwork!.Category != null ? a.Artwork!.Category!.Name : null,
                    CreatorProfileId = a.Artwork!.CreatorProfileId,
                    Tags = a.Artwork!.ArtworkTags.Where(t => t.Tag != null).Select(t => t.Tag!.Name).ToList(),
                    CurrentPrice = a.CurrentPrice,
                    BidCount = a.BidCount,
                    WatchCount = a.Watches.Count(w => !w.IsDeleted),
                    StartAt = a.StartAt,
                    EndAt = a.EndAt,
                    Status = a.Status,
                    HasPhysicalCopy = a.HasPhysicalCopy,
                    PhysicalDescription = a.PhysicalDescription,
                    FinalPrice = a.FinalPrice,
                    RiskLevel = a.RiskLevel
                })
                .ToListAsync(cancellationToken);

            rows.AddRange(extra);
        }

        return rows;
    }

    internal sealed class CandidateRow
    {
        public Guid AuctionId { get; init; }
        public Guid ArtworkId { get; init; }
        public string? Title { get; init; }
        public string? ThumbnailUrl { get; init; }
        public string? ImageUrl { get; init; }
        public string? Style { get; init; }
        public string? CategoryName { get; init; }
        public Guid CreatorProfileId { get; init; }
        public IReadOnlyList<string> Tags { get; init; } = [];
        public decimal CurrentPrice { get; init; }
        public int BidCount { get; init; }
        public int WatchCount { get; init; }
        public DateTimeOffset StartAt { get; init; }
        public DateTimeOffset EndAt { get; init; }
        public AuctionStatus Status { get; init; }
        public bool HasPhysicalCopy { get; init; }
        public string? PhysicalDescription { get; init; }
        public decimal? FinalPrice { get; init; }
        public AuctionRiskLevel? RiskLevel { get; init; }
    }

    internal sealed record ScoredCandidate(
        CandidateRow Candidate,
        double Score,
        string Reason,
        IReadOnlyList<string> Signals)
    {
        public Guid CreatorProfileId => Candidate.CreatorProfileId;

        public string? CategoryName => Candidate.CategoryName;

        public AuctionListItemDto ToListItemDto() => new(
            AuctionId: Candidate.AuctionId,
            Artwork: new AuctionArtworkDto(
                Candidate.ArtworkId,
                Candidate.Title ?? "Tác phẩm",
                Candidate.ThumbnailUrl,
                Candidate.ImageUrl,
                Candidate.Style),
            CurrentPrice: Candidate.CurrentPrice,
            BidCount: Candidate.BidCount,
            StartAt: Candidate.StartAt,
            EndAt: Candidate.EndAt,
            AuctionStatus: Candidate.Status.ToString(),
            WatchCount: Candidate.WatchCount,
            HasPhysicalCopy: Candidate.HasPhysicalCopy,
            PhysicalDescription: Candidate.PhysicalDescription,
            FinalPrice: Candidate.FinalPrice);
    }
}
