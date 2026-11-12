using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using INotificationPublisher = ArtCommission.Application.Notifications.Common.INotificationPublisher;

namespace ArtCommission.Application.Auction.Commands.Delivery;

/// <summary>
/// Người mua đánh giá người bán sau khi giao dịch đấu giá đã khép lại.
///
/// VÌ SAO đánh giá nằm ở đây chứ không ở marketplace: khi ký quỹ đã giải ngân thì sàn không
/// thu hồi được tiền, nên đánh giá là công cụ duy nhất còn lại để người mua phản ánh chất lượng
/// người bán và cảnh báo người mua sau. Mỗi phiên chỉ được đánh giá một lần.
/// </summary>
public record RateAuctionSellerCommand(
    Guid UserId,
    Guid AuctionId,
    int Rating,
    string? Comment = null
) : IRequest<(bool Success, AuctionSellerRatingDto? Data, string[] Errors)>;

public sealed record AuctionSellerRatingDto(
    Guid ReviewId,
    Guid AuctionId,
    Guid SellerId,
    Guid CreatorProfileId,
    int Rating,
    string? Comment,
    DateTimeOffset CreatedAt,
    decimal SellerRatingAverage,
    int SellerRatingCount
);

public class RateAuctionSellerCommandHandler
    : IRequestHandler<RateAuctionSellerCommand, (bool, AuctionSellerRatingDto?, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<RateAuctionSellerCommandHandler> _logger;

    public RateAuctionSellerCommandHandler(
        IApplicationDbContext db,
        INotificationPublisher notifications,
        ILogger<RateAuctionSellerCommandHandler> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<(bool, AuctionSellerRatingDto?, string[])> Handle(
        RateAuctionSellerCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Rating is < 1 or > 5)
        {
            return (false, null, ["Điểm đánh giá phải từ 1 đến 5 sao."]);
        }

        if (request.Comment is { Length: > 1000 })
        {
            return (false, null, ["Nội dung đánh giá tối đa 1000 ký tự."]);
        }

        var auction = await _db.Auctions
            .FirstOrDefaultAsync(a => a.Id == request.AuctionId && !a.IsDeleted, cancellationToken);

        if (auction is null)
        {
            return (false, null, ["Không tìm thấy phiên đấu giá."]);
        }

        if (auction.WinnerId != request.UserId)
        {
            return (false, null, ["Chỉ người thắng phiên mới có quyền đánh giá người bán."]);
        }

        var delivery = await _db.Set<Domain.Entities.Auction.AuctionDelivery>()
            .FirstOrDefaultAsync(d => d.AuctionId == auction.Id && !d.IsDeleted, cancellationToken);

        // Chỉ đánh giá khi giao dịch đã khép lại: phiên Completed, đơn đã giao xong, hoặc khiếu nại
        // đã được Admin đóng (kể cả trường hợp không hoàn được tiền).
        var transactionClosed =
            auction.Status == AuctionStatus.Completed
            || delivery is { Status: DeliveryStatus.Delivered or DeliveryStatus.Resolved }
            || delivery?.DisputeResolvedAt is not null;

        if (!transactionClosed)
        {
            return (false, null,
                ["Chỉ có thể đánh giá người bán sau khi giao dịch đã hoàn tất hoặc khiếu nại đã được xử lý."]);
        }

        var sellerProfile = await _db.CreatorProfiles
            .FirstOrDefaultAsync(p => p.UserId == auction.SellerId && !p.IsDeleted, cancellationToken);

        if (sellerProfile is null)
        {
            return (false, null, ["Người bán chưa có hồ sơ nghệ sĩ nên chưa thể đánh giá."]);
        }

        var alreadyRated = await _db.CreatorReviews
            .AnyAsync(r => r.AuctionId == auction.Id
                           && r.ReviewerUserId == request.UserId
                           && !r.IsDeleted,
                cancellationToken);

        if (alreadyRated)
        {
            return (false, null, ["Bạn đã đánh giá người bán cho phiên này rồi."]);
        }

        var review = new CreatorReview
        {
            CreatorProfileId = sellerProfile.Id,
            ReviewerUserId = request.UserId,
            Rating = request.Rating,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            AuctionId = auction.Id,
            Source = "auction"
        };

        _db.CreatorReviews.Add(review);
        await _db.SaveChangesAsync(cancellationToken);

        // Cập nhật điểm trung bình trên hồ sơ nghệ sĩ (marketplace/search đọc RatingAverage).
        var ratings = await _db.CreatorReviews
            .Where(r => r.CreatorProfileId == sellerProfile.Id && !r.IsDeleted)
            .Select(r => r.Rating)
            .ToListAsync(cancellationToken);

        sellerProfile.RatingCount = ratings.Count;
        sellerProfile.RatingAverage = ratings.Count == 0
            ? 0m
            : Math.Round((decimal)ratings.Average(), 2);
        sellerProfile.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Người mua {BuyerId} đánh giá người bán {SellerId} phiên {AuctionId}: {Rating} sao.",
            request.UserId, auction.SellerId, auction.Id, request.Rating);

        try
        {
            await _notifications.PublishAsync(
                userId: auction.SellerId,
                notificationType: NotificationType.AuctionLifecycle,
                title: "Bạn nhận được đánh giá mới",
                body: $"Người mua phiên #{auction.Id.ToString()[..8].ToUpperInvariant()} đã đánh giá bạn {request.Rating}/5 sao. Điểm trung bình hiện tại: {sellerProfile.RatingAverage:0.00} ({sellerProfile.RatingCount} đánh giá).",
                refType: "Auction",
                refId: auction.Id,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi gửi thông báo đánh giá cho Seller {SellerId}", auction.SellerId);
        }

        return (true, new AuctionSellerRatingDto(
            ReviewId: review.Id,
            AuctionId: auction.Id,
            SellerId: auction.SellerId,
            CreatorProfileId: sellerProfile.Id,
            Rating: review.Rating,
            Comment: review.Comment,
            CreatedAt: review.CreatedAt,
            SellerRatingAverage: sellerProfile.RatingAverage,
            SellerRatingCount: sellerProfile.RatingCount), []);
    }
}
