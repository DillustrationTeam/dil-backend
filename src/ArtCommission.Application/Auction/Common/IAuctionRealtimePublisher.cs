using ArtCommission.Application.Auction.DTOs;

namespace ArtCommission.Application.Auction.Common;

public interface IAuctionRealtimePublisher
{
    /// <summary>
    /// Phát sự kiện có lượt đặt giá mới.
    /// </summary>
    /// <param name="auctionStatus">
    /// Trạng thái THẬT của phiên tại thời điểm phát. Trước đây publisher hard-code "Active",
    /// nên lượt đặt giá chạm giá mua ngay (phiên đã chốt) vẫn báo "Active" cho cả phòng.
    /// </param>
    /// <param name="leadingBidderId">
    /// Người đang dẫn đầu sau lượt này. Có thể KHÁC người vừa gửi lệnh khi proxy/auto-bid nâng
    /// giá thay người khác — client dùng để hiển thị đúng ai đang dẫn đầu.
    /// </param>
    Task PublishBidPlacedAsync(
        Guid auctionId,
        PlaceBidResultDto bid,
        decimal currentPrice,
        decimal minimumNextBid,
        DateTimeOffset endAt,
        CancellationToken cancellationToken,
        string? auctionStatus = null,
        Guid? leadingBidderId = null);

    Task PublishAuctionExtendedAsync(Guid auctionId, Guid bidId, DateTimeOffset oldEndAt, DateTimeOffset newEndAt, CancellationToken cancellationToken);
    Task PublishAuctionStatusChangedAsync(Guid auctionId, string status, decimal currentPrice, DateTimeOffset endAt, CancellationToken cancellationToken);
}
