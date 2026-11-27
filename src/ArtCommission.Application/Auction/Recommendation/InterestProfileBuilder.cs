using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Auction;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Recommendation;

/// <summary>
/// Builds and caches the interest profile of one bidder.
///
/// Behaviour weights (deliberately different per action, because they mean different things):
///   bid 5 · watch 4 · follow creator 3 · favourite 3 · collection save 2
///
/// Every contribution is decayed with a 45-day half-life: taste moves on, and a bid from last
/// spring should not decide today's rail. The profile is persisted in <c>UserInterestProfiles</c>
/// and reused for 12 hours so the rail stays a single fast read.
/// </summary>
public interface IInterestProfileBuilder
{
    Task<InterestProfile> GetOrBuildAsync(Guid userId, bool forceRebuild, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the cached profile stale after a strong signal (bid, watch) so the next rail read
    /// rebuilds it. Cheap enough to call inside the bid command: one row update, no recompute.
    /// </summary>
    Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class InterestProfileBuilder(IApplicationDbContext db) : IInterestProfileBuilder
{
    private const int BidWeight = 5;
    private const int WatchWeight = 4;
    private const int FollowWeight = 3;
    private const int FavoriteWeight = 3;
    private const int CollectionWeight = 2;
    private const int MaxHistoryRows = 200;

    /// <summary>Creator affinity gained by touching one of their artworks (follows count double).</summary>
    private const decimal ArtworkCreatorShare = 0.5m;

    /// <summary>Days until a contribution counts half as much.</summary>
    private const double HalfLifeDays = 45d;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(12);

    public async Task<InterestProfile> GetOrBuildAsync(
        Guid userId,
        bool forceRebuild,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return InterestProfile.Empty;
        }

        var now = DateTimeOffset.UtcNow;

        if (!forceRebuild)
        {
            var cached = await db.UserInterestProfiles.AsNoTracking()
                .Where(p => p.UserId == userId && !p.IsDeleted && p.ExpiresAt > now)
                .Select(p => p.ProfileJson)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(cached))
            {
                return InterestProfile.Deserialize(cached);
            }
        }

        var profile = await BuildAsync(userId, now, cancellationToken);

        var row = await db.UserInterestProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted, cancellationToken);

        if (row is null)
        {
            row = new UserInterestProfile { UserId = userId };
            db.UserInterestProfiles.Add(row);
        }

        row.ProfileJson = profile.Serialize();
        row.SourceEventCount = profile.SourceEventCount;
        row.Version += 1;
        row.BuiltAt = now;
        row.ExpiresAt = now.Add(CacheTtl);
        row.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);
        return profile;
    }

    public async Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return;
        }

        var row = await db.UserInterestProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted, cancellationToken);

        if (row is null)
        {
            return;
        }

        // Expire instead of deleting: the weights stay readable for debugging while the next
        // GetOrBuildAsync rebuilds from fresh behaviour.
        row.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>One artwork contribution before aggregation.</summary>
    private readonly record struct ArtworkSignal(Guid ArtworkId, decimal Weight);

    private async Task<InterestProfile> BuildAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var signals = new List<ArtworkSignal>();
        var events = 0;

        // ---- bids: the strongest intent signal, and the only source of a price band
        var bids = await db.Bids.AsNoTracking()
            .Where(b => b.BidderId == userId && !b.IsDeleted)
            .OrderByDescending(b => b.PlacedAt)
            .Take(MaxHistoryRows)
            .Select(b => new { b.AuctionId, b.PlacedAt, b.Amount })
            .ToListAsync(cancellationToken);

        var auctionToArtwork = await ArtworkIdsOfAuctionsAsync(
            bids.Select(b => b.AuctionId), cancellationToken);

        foreach (var bid in bids)
        {
            if (!auctionToArtwork.TryGetValue(bid.AuctionId, out var artworkId))
            {
                continue;
            }

            events++;
            signals.Add(new ArtworkSignal(artworkId, BidWeight * Decay(bid.PlacedAt, now)));
        }

        // ---- watches: interest without a commitment
        var watches = await db.AuctionWatches.AsNoTracking()
            .Where(w => w.UserId == userId && !w.IsDeleted)
            .OrderByDescending(w => w.CreatedAt)
            .Take(MaxHistoryRows)
            .Select(w => new { w.AuctionId, w.CreatedAt })
            .ToListAsync(cancellationToken);

        var watchedArtworks = await ArtworkIdsOfAuctionsAsync(
            watches.Select(w => w.AuctionId), cancellationToken);

        foreach (var watch in watches)
        {
            if (!watchedArtworks.TryGetValue(watch.AuctionId, out var artworkId))
            {
                continue;
            }

            events++;
            signals.Add(new ArtworkSignal(artworkId, WatchWeight * Decay(watch.CreatedAt, now)));
        }

        // ---- favourites and collection saves: browsing taste
        var favorites = await db.ArtworkFavorites.AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Take(MaxHistoryRows)
            .Select(f => new { f.ArtworkId, f.CreatedAt })
            .ToListAsync(cancellationToken);

        foreach (var favorite in favorites)
        {
            events++;
            signals.Add(new ArtworkSignal(favorite.ArtworkId, FavoriteWeight * Decay(favorite.CreatedAt, now)));
        }

        var saved = await db.CollectionArtworks.AsNoTracking()
            .Where(c => c.Collection!.OwnerUserId == userId && !c.Collection.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .Take(MaxHistoryRows)
            .Select(c => new { c.ArtworkId, c.CreatedAt })
            .ToListAsync(cancellationToken);

        foreach (var item in saved)
        {
            events++;
            signals.Add(new ArtworkSignal(item.ArtworkId, CollectionWeight * Decay(item.CreatedAt, now)));
        }

        var tags = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var styles = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var categories = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var creators = new Dictionary<Guid, decimal>();

        // One query per batch instead of one per artwork: the rail must stay a cheap read.
        foreach (var group in signals.GroupBy(s => s.ArtworkId))
        {
            var weight = group.Sum(s => s.Weight);
            if (weight <= 0)
            {
                continue;
            }

            var artwork = await LoadArtworkSignalAsync(group.Key, cancellationToken);
            if (artwork is null)
            {
                continue;
            }

            foreach (var tag in artwork.Tags)
            {
                tags[tag] = tags.GetValueOrDefault(tag) + weight;
            }

            if (!string.IsNullOrWhiteSpace(artwork.Style))
            {
                styles[artwork.Style] = styles.GetValueOrDefault(artwork.Style) + weight;
            }

            if (!string.IsNullOrWhiteSpace(artwork.CategoryName))
            {
                categories[artwork.CategoryName] = categories.GetValueOrDefault(artwork.CategoryName) + weight;
            }

            creators[artwork.CreatorProfileId] =
                creators.GetValueOrDefault(artwork.CreatorProfileId) + (weight * ArtworkCreatorShare);
        }

        // ---- follows: creator affinity, independent of any single artwork
        var follows = await db.Follows.AsNoTracking()
            .Where(f => f.FollowerUserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Take(MaxHistoryRows)
            .Select(f => new { f.CreatorProfileId, f.CreatedAt })
            .ToListAsync(cancellationToken);

        foreach (var follow in follows)
        {
            events++;
            var weight = FollowWeight * Decay(follow.CreatedAt, now);
            creators[follow.CreatorProfileId] = creators.GetValueOrDefault(follow.CreatorProfileId) + weight;
        }

        var median = MedianBidAmount(bids.Select(b => b.Amount).ToList());

        return new InterestProfile(tags, styles, categories, creators, median, events, now);
    }

    private sealed record ArtworkSignalRow(string? Style, string? CategoryName, Guid CreatorProfileId, IReadOnlyList<string> Tags);

    private async Task<ArtworkSignalRow?> LoadArtworkSignalAsync(Guid artworkId, CancellationToken cancellationToken)
    {
        var row = await db.Artworks.AsNoTracking()
            .Where(a => a.Id == artworkId && !a.IsDeleted)
            .Select(a => new
            {
                a.Style,
                CategoryName = a.Category != null ? a.Category.Name : null,
                a.CreatorProfileId,
                Tags = a.ArtworkTags.Where(t => t.Tag != null).Select(t => t.Tag!.Name).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new ArtworkSignalRow(row.Style, row.CategoryName, row.CreatorProfileId, row.Tags);
    }

    /// <summary>
    /// Maps auction id -> artwork id. Batched once per source so the builder stays at a handful of
    /// queries regardless of how much history the user has.
    /// </summary>
    private async Task<Dictionary<Guid, Guid>> ArtworkIdsOfAuctionsAsync(
        IEnumerable<Guid> auctionIds,
        CancellationToken cancellationToken)
    {
        var ids = auctionIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await db.Auctions.AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .Select(a => new { a.Id, a.ArtworkId })
            .ToDictionaryAsync(x => x.Id, x => x.ArtworkId, cancellationToken);
    }

    private static decimal Decay(DateTimeOffset at, DateTimeOffset now)
    {
        var days = Math.Max(0d, (now - at).TotalDays);
        return (decimal)Math.Exp(-days / HalfLifeDays);
    }

    private static decimal MedianBidAmount(IReadOnlyList<decimal> amounts)
    {
        if (amounts.Count == 0)
        {
            return 0m;
        }

        var sorted = amounts.OrderBy(a => a).ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2m;
    }
}
