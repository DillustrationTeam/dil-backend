using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Auction.Common;

/// <summary>
/// Chuyển entity đấu giá sang DTO. Gom một chỗ để mọi endpoint trả cùng một hình dạng
/// dữ liệu — FE chỉ viết một mapper.
/// </summary>
public static class AuctionMapper
{
    public static AuctionArtworkDto? ToArtworkDto(Artwork? artwork) =>
        artwork is null
            ? null
            : new AuctionArtworkDto(
                artwork.Id,
                artwork.Title,
                artwork.ThumbnailUrl ?? artwork.ImageUrl,
                artwork.ImageUrl,
                artwork.Style);

    public static AuctionSellerDto? ToSellerDto(ApplicationUser? user) =>
        user is null
            ? null
            : new AuctionSellerDto(user.Id, user.FullName);

    /// <param name="originalFileUrl">
    /// Chỉ truyền giá trị khi người gọi ĐƯỢC xem file gốc (seller hoặc winner đã thanh toán).
    /// Truyền null cho mọi trường hợp khác — đây là điểm bảo vệ tác quyền, không phải chi tiết hiển thị.
    /// </param>
    public static AuctionDto ToDto(
        Domain.Entities.Auction.Auction auction,
        string? originalFileUrl = null) => new(
        AuctionId: auction.Id,
        ArtworkId: auction.ArtworkId,
        SellerId: auction.SellerId,
        AuctionType: auction.AuctionType.ToString(),
        StartPrice: auction.StartPrice,
        ReservePrice: auction.ReservePrice,
        BidStep: auction.BidStep,
        BuyNowPrice: auction.BuyNowPrice,
        CurrentPrice: auction.CurrentPrice,
        BidCount: auction.BidCount,
        StartAt: auction.StartAt,
        EndAt: auction.EndAt,
        AuctionStatus: auction.Status.ToString(),
        WinnerId: auction.WinnerId,
        FinalPrice: auction.FinalPrice,
        SettledAt: auction.SettledAt,
        PaymentDeadline: auction.PaymentDeadline,
        CancelReason: auction.CancelReason,
        OriginalFileUrl: originalFileUrl);

    public static AuctionListItemDto ToListItemDto(
        Domain.Entities.Auction.Auction auction,
        Artwork? artwork,
        int watchCount) => new(
        AuctionId: auction.Id,
        Artwork: ToArtworkDto(artwork),
        CurrentPrice: auction.CurrentPrice,
        BidCount: auction.BidCount,
        EndAt: auction.EndAt,
        AuctionStatus: auction.Status.ToString(),
        WatchCount: watchCount);

    public static BidDto ToDto(Bid bid, string? bidderFullName = null) => new(
        BidId: bid.Id,
        AuctionId: bid.AuctionId,
        BidderId: bid.BidderId,
        BidderFullName: bidderFullName,
        // Đặc tả API ghi `bidder: { userId, fullName }` — trả cả dạng lồng này để FE
        // đọc đúng hợp đồng, đồng thời giữ `bidderId` phẳng cho client đang dùng bản cũ.
        Bidder: new BidderDto(bid.BidderId, bidderFullName),
        Amount: bid.Amount,
        HoldAmount: bid.HoldAmount,
        BidStatus: bid.Status.ToString(),
        HoldStatus: bid.HoldStatus.ToString(),
        PlacedAt: bid.PlacedAt);

    /// <summary>Nhãn hiển thị cho enum trạng thái — dùng trong thông báo, không dùng cho API.</summary>
    public static string DescribeStatus(AuctionStatus status) => status switch
    {
        AuctionStatus.Scheduled => "đã lên lịch",
        AuctionStatus.Active => "đang diễn ra",
        AuctionStatus.Ended => "đã kết thúc",
        AuctionStatus.Settled => "đã chốt",
        AuctionStatus.Cancelled => "đã huỷ",
        AuctionStatus.Expired => "hết hạn thanh toán",
        _ => status.ToString()
    };
}
