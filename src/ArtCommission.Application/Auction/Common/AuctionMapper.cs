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
                artwork.ThumbnailUrl ?? artwork.WatermarkedUrl ?? artwork.ImageUrl,
                artwork.WatermarkedUrl ?? artwork.ImageUrl,
                artwork.Style,
                artwork.CreatedAt,
                artwork.Resolution,
                artwork.FileSizeBytes,
                GetFileFormat(artwork.ImageUrl));

    private static string? GetFileFormat(string? fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl)) return null;

        var path = Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri)
            ? uri.AbsolutePath
            : fileUrl.Split('?', '#')[0];
        var extension = Path.GetExtension(path);
        return string.IsNullOrWhiteSpace(extension) ? null : extension.TrimStart('.').ToUpperInvariant();
    }

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
        OriginalFileUrl: originalFileUrl,
        HasPhysicalCopy: auction.HasPhysicalCopy,
        PhysicalDescription: auction.PhysicalDescription,
        RequiresPhysicalDelivery: auction.RequiresPhysicalDelivery,
        PhysicalChoiceDeadline: auction.PhysicalChoiceDeadline);

    public static AuctionListItemDto ToListItemDto(
        Domain.Entities.Auction.Auction auction,
        Artwork? artwork,
        int watchCount) => new(
        AuctionId: auction.Id,
        Artwork: ToArtworkDto(artwork),
        CurrentPrice: auction.CurrentPrice,
        BidCount: auction.BidCount,
        StartAt: auction.StartAt,
        EndAt: auction.EndAt,
        AuctionStatus: auction.Status.ToString(),
        WatchCount: watchCount,
        HasPhysicalCopy: auction.HasPhysicalCopy,
        PhysicalDescription: auction.PhysicalDescription);

    public static AuctionDeliveryDto? ToDeliveryDto(AuctionDelivery? delivery) =>
        delivery is null
            ? null
            : new AuctionDeliveryDto(
                Id: delivery.Id,
                AuctionId: delivery.AuctionId,
                RecipientId: delivery.RecipientId,
                RecipientName: delivery.RecipientName,
                RecipientPhone: delivery.RecipientPhone,
                ShippingAddress: delivery.ShippingAddress,
                NoteToSeller: delivery.NoteToSeller,
                CarrierName: delivery.CarrierName,
                TrackingCode: delivery.TrackingCode,
                TrackingUrl: delivery.TrackingUrl,
                ShippingReceiptUrl: delivery.ShippingReceiptUrl,
                PackageImageUrl: delivery.PackageImageUrl,
                DeliveryReceiptUrl: delivery.DeliveryReceiptUrl,
                Status: delivery.Status.ToString(),
                ShippedAt: delivery.ShippedAt,
                DeliveredAt: delivery.DeliveredAt,
                ConfirmedAt: delivery.ConfirmedAt,
                DisputeReason: delivery.DisputeReason,
                DisputeEvidenceUrl: delivery.DisputeEvidenceUrl);

    public static IReadOnlyList<AuctionReceiptDto> BuildReceipts(
        Domain.Entities.Auction.Auction auction,
        EscrowTransaction? escrow,
        AuctionDelivery? delivery)
    {
        var receipts = new List<AuctionReceiptDto>();
        var shortId = auction.Id.ToString()[..8].ToUpperInvariant();

        if (escrow is not null && auction.WinnerId.HasValue)
        {
            receipts.Add(new AuctionReceiptDto(
                ReceiptType: "EscrowDeposit",
                ReceiptNumber: $"REC-ESC-{shortId}",
                IssuedAt: escrow.CreatedAt,
                Amount: escrow.Amount,
                FeeAmount: escrow.FeeAmount,
                NetAmount: escrow.Amount,
                Title: "Biên lai Tạm giữ Tiền Ký quỹ Đấu giá",
                ProofImageUrl: null,
                Description: $"Khoản tiền trúng đấu giá phiên {auction.Id} đang được sàn Dillustration bảo chứng ký quỹ an toàn.",
                PayerId: escrow.PayerId,
                PayeeId: escrow.PayeeId));
        }

        if (delivery is not null && !string.IsNullOrWhiteSpace(delivery.ShippingReceiptUrl))
        {
            receipts.Add(new AuctionReceiptDto(
                ReceiptType: "CarrierShipping",
                ReceiptNumber: $"SHIP-{(string.IsNullOrWhiteSpace(delivery.TrackingCode) ? shortId : delivery.TrackingCode)}",
                IssuedAt: delivery.ShippedAt ?? delivery.CreatedAt,
                Amount: 0m,
                FeeAmount: null,
                NetAmount: null,
                Title: $"Phiếu gửi bưu cục ({delivery.CarrierName ?? "Vận chuyển"})",
                ProofImageUrl: delivery.ShippingReceiptUrl,
                Description: $"Mã vận đơn: {delivery.TrackingCode}. Kiện hàng đã bàn giao cho bưu cục vận chuyển.",
                PayerId: auction.SellerId,
                PayeeId: delivery.RecipientId));
        }

        if (delivery is not null && !string.IsNullOrWhiteSpace(delivery.DeliveryReceiptUrl))
        {
            receipts.Add(new AuctionReceiptDto(
                ReceiptType: "ProofOfDelivery",
                ReceiptNumber: $"POD-{shortId}",
                IssuedAt: delivery.DeliveredAt ?? delivery.UpdatedAt ?? delivery.CreatedAt,
                Amount: 0m,
                FeeAmount: null,
                NetAmount: null,
                Title: "Biên bản Phát hàng & Ký nhận (PoD)",
                ProofImageUrl: delivery.DeliveryReceiptUrl,
                Description: "Bằng chứng người mua đã nhận bưu kiện từ bưu tá chuyển phát.",
                PayerId: delivery.RecipientId,
                PayeeId: auction.SellerId));
        }

        if (escrow is not null && escrow.Status == EscrowStatus.Released && escrow.ReleasedAt.HasValue)
        {
            receipts.Add(new AuctionReceiptDto(
                ReceiptType: "SettlementPayout",
                ReceiptNumber: $"REC-SET-{shortId}",
                IssuedAt: escrow.ReleasedAt.Value,
                Amount: escrow.Amount,
                FeeAmount: escrow.FeeAmount,
                NetAmount: escrow.ReleasedAmount,
                Title: "Biên lai Quyết toán Giao dịch Hoàn tất",
                ProofImageUrl: null,
                Description: $"Đã quyết toán hoàn tất phiên {auction.Id}. Số tiền thực nhận của Người bán: {escrow.ReleasedAmount:N0} VND sau khi trừ phí nền tảng {escrow.FeeAmount:N0} VND.",
                PayerId: escrow.PayerId,
                PayeeId: escrow.PayeeId));
        }

        return receipts;
    }

    public static BidDto ToDto(Bid bid, string? bidderFullName = null) => new(
        BidId: bid.Id,
        AuctionId: bid.AuctionId,
        BidderId: bid.BidderId,
        BidderFullName: bidderFullName,
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
        AuctionStatus.AwaitingPhysical => "chờ xác nhận tranh vật lý",
        AuctionStatus.InTransit => "đang giao tranh vật lý",
        AuctionStatus.Completed => "hoàn tất",
        _ => status.ToString()
    };
}
