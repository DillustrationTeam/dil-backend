namespace ArtCommission.Application.Auction.DTOs;

/// <summary>
/// One personalised card: the exact same auction payload the marketplace list uses, plus the score,
/// the Vietnamese reason line and the signals that produced it (for debugging and for the FE chip).
/// </summary>
public sealed record RecommendedAuctionDto(
    AuctionListItemDto Auction,
    decimal Score,
    string Reason,
    IReadOnlyList<string> Signals);

/// <summary>One card the rail actually rendered, with the score/rank the client saw.</summary>
public sealed record RecommendationImpressionItem(Guid AuctionId, decimal? Score = null, int? Rank = null);

/// <summary>
/// Body of POST /auctions/recommendations/impressions (batch, max 30).
///
/// <see cref="Items"/> is the preferred shape - it keeps score/rank for offline analysis.
/// <see cref="AuctionIds"/> stays supported so an older client bundle keeps working.
/// </summary>
public sealed record RecommendationImpressionRequest(
    IReadOnlyList<Guid>? AuctionIds = null,
    string? Surface = null,
    bool Clicked = false,
    IReadOnlyList<RecommendationImpressionItem>? Items = null);
