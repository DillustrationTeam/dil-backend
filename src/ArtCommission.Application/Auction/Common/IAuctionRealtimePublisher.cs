using ArtCommission.Application.Auction.DTOs;

namespace ArtCommission.Application.Auction.Common;

public interface IAuctionRealtimePublisher
{
    Task PublishBidPlacedAsync(Guid auctionId, PlaceBidResultDto bid, decimal currentPrice, decimal minimumNextBid, DateTimeOffset endAt, CancellationToken cancellationToken);
    Task PublishAuctionExtendedAsync(Guid auctionId, Guid bidId, DateTimeOffset oldEndAt, DateTimeOffset newEndAt, CancellationToken cancellationToken);
    Task PublishAuctionStatusChangedAsync(Guid auctionId, string status, decimal currentPrice, DateTimeOffset endAt, CancellationToken cancellationToken);
}
