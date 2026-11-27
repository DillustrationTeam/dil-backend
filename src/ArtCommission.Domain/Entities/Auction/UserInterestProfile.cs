using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Auction;

/// <summary>
/// Cached interest profile of one bidder, used by the personalised auction rail.
///
/// Storing the aggregated weights instead of recomputing them per request keeps the rail fast; the
/// raw behaviour (bids, watches, favourites, collections, follows) stays the single source of truth
/// and is re-read whenever <see cref="ExpiresAt"/> passes or the caller forces a rebuild.
/// </summary>
public class UserInterestProfile : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>Serialised weight maps: tags, styles, categories, creators, plus the bid-price median.</summary>
    public string ProfileJson { get; set; } = "{}";

    /// <summary>How many behaviour rows fed the profile - a cheap staleness signal.</summary>
    public int SourceEventCount { get; set; }

    /// <summary>Incremented on every rebuild; recommendation caches key on it.</summary>
    public int Version { get; set; } = 1;

    public DateTimeOffset BuiltAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddHours(12);
}

/// <summary>
/// One "this auction was shown to this user" record, used to measure the rail (CTR) and to keep a
/// surface from repeating the same card on every visit.
///
/// Append-only; <see cref="ClickedAt"/> is the only mutable field. Anonymous visitors are recorded
/// with <see cref="UserId"/> = Guid.Empty so trending rows still have an impression count.
/// </summary>
public class RecommendationImpression : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid AuctionId { get; set; }

    /// <summary>Where the card was rendered: auction-list, detail-similar, my-auctions.</summary>
    public string Surface { get; set; } = "auction-list";

    public decimal Score { get; set; }
    public int Rank { get; set; }

    /// <summary>Vietnamese reason shown on the card, kept for offline analysis of what convinces.</summary>
    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset ShownAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClickedAt { get; set; }

    // Navigation
    public Auction? Auction { get; set; }
}
