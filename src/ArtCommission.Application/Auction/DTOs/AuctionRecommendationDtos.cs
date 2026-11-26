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

/// <summary>Body of POST /auctions/recommendations/impressions (batch, max 30 ids).</summary>
public sealed record RecommendationImpressionRequest(
    IReadOnlyList<Guid> AuctionIds,
    string? Surface = null,
    bool Clicked = false);
