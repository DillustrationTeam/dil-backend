using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Auction.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace ArtCommission.API.Hubs;

public sealed class AuctionRealtimePublisher : IAuctionRealtimePublisher
{
    private readonly IHubContext<AuctionHub> _hub;

    public AuctionRealtimePublisher(IHubContext<AuctionHub> hub) => _hub = hub;

    public Task PublishBidPlacedAsync(
        Guid auctionId,
        PlaceBidResultDto bid,
        decimal currentPrice,
        decimal minimumNextBid,
        DateTimeOffset endAt,
        CancellationToken cancellationToken,
        string? auctionStatus = null,
        Guid? leadingBidderId = null) =>
        _hub.Clients.Group(AuctionHub.GroupName(auctionId)).SendAsync("BidPlaced", new
        {
            auctionId,
            bid = bid.Bid,
            currentPrice,
            minimumNextBid,
            endAt,
            // Trạng thái thật của phiên (có thể đã chốt nếu lượt này chạm giá mua ngay),
            // thay vì hard-code "Active" như trước.
            auctionStatus = auctionStatus ?? "Active",
            // Người đang dẫn đầu có thể khác người gửi lệnh khi auto-bid nâng giá thay họ.
            leadingBidderId
        }, cancellationToken);

    public Task PublishAuctionExtendedAsync(Guid auctionId, Guid bidId, DateTimeOffset oldEndAt, DateTimeOffset newEndAt, CancellationToken cancellationToken) =>
        _hub.Clients.Group(AuctionHub.GroupName(auctionId)).SendAsync("AuctionExtended", new
        {
            auctionId,
            bidId,
            oldEndAt,
            endAt = newEndAt
        }, cancellationToken);

    public Task PublishAuctionStatusChangedAsync(Guid auctionId, string status, decimal currentPrice, DateTimeOffset endAt, CancellationToken cancellationToken) =>
        _hub.Clients.Group(AuctionHub.GroupName(auctionId)).SendAsync("AuctionStatusChanged", new
        {
            auctionId,
            status,
            currentPrice,
            endAt
        }, cancellationToken);
}
