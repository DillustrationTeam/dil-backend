using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Queries.GetPhysicalDeliveries;

public sealed record GetPhysicalDeliveriesQuery(
    Guid? CurrentUserId,
    bool IsAdmin = false,
    string? Role = null,
    string? Status = null
) : IRequest<(bool Success, IReadOnlyList<PhysicalDeliveryListItemDto>? Data, string[] Errors)>;

public sealed class GetPhysicalDeliveriesQueryHandler
    : IRequestHandler<GetPhysicalDeliveriesQuery, (bool, IReadOnlyList<PhysicalDeliveryListItemDto>?, string[])>
{
    private readonly IApplicationDbContext _db;

    public GetPhysicalDeliveriesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, IReadOnlyList<PhysicalDeliveryListItemDto>?, string[])> Handle(
        GetPhysicalDeliveriesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _db.Auctions
            .AsNoTracking()
            .Include(a => a.Artwork)
            .Where(a => a.HasPhysicalCopy && !a.IsDeleted);

        // Phân quyền: nếu không phải Admin thì chỉ xem đơn của chính mình (vai trò Seller hoặc Winner)
        if (!request.IsAdmin)
        {
            if (request.CurrentUserId == null || request.CurrentUserId == Guid.Empty)
            {
                return (false, null, ["Yêu cầu đăng nhập để xem danh sách đơn giao nhận."]);
            }

            var userId = request.CurrentUserId.Value;
            var role = request.Role?.ToLowerInvariant();

            if (role == "seller")
            {
                query = query.Where(a => a.SellerId == userId);
            }
            else if (role == "winner" || role == "buyer")
            {
                query = query.Where(a => a.WinnerId == userId);
            }
            else
            {
                query = query.Where(a => a.SellerId == userId || a.WinnerId == userId);
            }
        }

        var auctions = await query
            .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
            .ToListAsync(cancellationToken);

        if (auctions.Count == 0)
        {
            return (true, Array.Empty<PhysicalDeliveryListItemDto>(), []);
        }

        var auctionIds = auctions.Select(a => a.Id).ToList();

        // Lấy thông tin user (Seller và Winner)
        var userIds = auctions
            .Select(a => a.SellerId)
            .Concat(auctions.Where(a => a.WinnerId.HasValue).Select(a => a.WinnerId!.Value))
            .Distinct()
            .ToList();

        var userDict = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName ?? u.UserName ?? "Người dùng", cancellationToken);

        // Lấy thông tin vận chuyển
        var deliveries = await _db.Set<AuctionDelivery>()
            .AsNoTracking()
            .Where(d => auctionIds.Contains(d.AuctionId) && !d.IsDeleted)
            .ToDictionaryAsync(d => d.AuctionId, cancellationToken);

        // Lấy trạng thái ký quỹ
        var escrows = await _db.Set<EscrowTransaction>()
            .AsNoTracking()
            .Where(e => auctionIds.Contains(e.AuctionId) && !e.IsDeleted)
            .GroupBy(e => e.AuctionId)
            .Select(g => g.OrderByDescending(e => e.CreatedAt).First())
            .ToDictionaryAsync(e => e.AuctionId, cancellationToken);

        // Phiên nào người dùng hiện tại đã đánh giá người bán rồi (mỗi phiên chỉ đánh giá 1 lần).
        var ratedAuctionIds = request.CurrentUserId is { } viewerId && viewerId != Guid.Empty
            ? await _db.CreatorReviews
                .AsNoTracking()
                .Where(r => r.AuctionId != null && r.ReviewerUserId == viewerId && !r.IsDeleted)
                .Select(r => r.AuctionId!.Value)
                .ToListAsync(cancellationToken)
            : new List<Guid>();

        // Hồ sơ nghệ sĩ của người bán — cần cho form đánh giá.
        var sellerIds = auctions.Select(a => a.SellerId).Distinct().ToList();
        var sellerProfileMap = await _db.CreatorProfiles
            .AsNoTracking()
            .Where(p => sellerIds.Contains(p.UserId) && !p.IsDeleted)
            .ToDictionaryAsync(p => p.UserId, p => p.Id, cancellationToken);

        var result = new List<PhysicalDeliveryListItemDto>();

        foreach (var a in auctions)
        {
            deliveries.TryGetValue(a.Id, out var delivery);
            escrows.TryGetValue(a.Id, out var escrow);

            // Người mua đánh giá được người bán khi giao dịch đã khép lại (hoàn tất, đã giao xong,
            // hoặc khiếu nại đã được Admin đóng) và chưa đánh giá cho phiên này.
            var hasRatedSeller = ratedAuctionIds.Contains(a.Id);
            var transactionClosed =
                a.Status == AuctionStatus.Completed
                || delivery is { Status: DeliveryStatus.Delivered or DeliveryStatus.Resolved }
                || delivery?.DisputeResolvedAt is not null;
            var canRateSeller = transactionClosed
                                && !hasRatedSeller
                                && a.WinnerId == request.CurrentUserId;

            var deliveryDto = AuctionMapper.ToDeliveryDto(delivery, canRateSeller, hasRatedSeller);
            var escrowStatusStr = escrow?.Status.ToString() ?? (a.Status == AuctionStatus.Active ? "BiddingHold" : "None");
            // Bộ lọc status nếu có
            if (!string.IsNullOrWhiteSpace(request.Status) && !string.Equals(request.Status, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                var targetStatus = request.Status.Trim();
                var matchDeliveryStatus = deliveryDto != null && string.Equals(deliveryDto.Status, targetStatus, StringComparison.OrdinalIgnoreCase);
                var matchAuctionStatus = string.Equals(a.Status.ToString(), targetStatus, StringComparison.OrdinalIgnoreCase);

                if (!matchDeliveryStatus && !matchAuctionStatus)
                {
                    continue;
                }
            }

            userDict.TryGetValue(a.SellerId, out var sellerName);
            string? winnerName = null;
            if (a.WinnerId.HasValue)
            {
                userDict.TryGetValue(a.WinnerId.Value, out winnerName);
            }

            result.Add(new PhysicalDeliveryListItemDto(
                AuctionId: a.Id,
                ArtworkId: a.ArtworkId,
                ArtworkTitle: a.Artwork?.Title ?? "Tác phẩm đấu giá",
                ArtworkImageUrl: a.Artwork?.ThumbnailUrl ?? a.Artwork?.ImageUrl,
                ArtworkStyle: a.Artwork?.Style,
                SellerId: a.SellerId,
                SellerName: sellerName ?? "Nghệ sĩ",
                WinnerId: a.WinnerId,
                WinnerName: winnerName,
                AuctionStatus: a.Status.ToString(),
                CurrentPrice: a.CurrentPrice,
                FinalPrice: a.FinalPrice,
                HasPhysicalCopy: a.HasPhysicalCopy,
                PhysicalDescription: a.PhysicalDescription,
                PhysicalChoiceDeadline: a.PhysicalChoiceDeadline,
                RequiresPhysicalDelivery: a.RequiresPhysicalDelivery,
                Delivery: deliveryDto,
                EscrowStatus: escrowStatusStr,
                SellerCreatorProfileId: sellerProfileMap.TryGetValue(a.SellerId, out var profileId) ? profileId : null));
        }

        return (true, result, []);
    }
}
