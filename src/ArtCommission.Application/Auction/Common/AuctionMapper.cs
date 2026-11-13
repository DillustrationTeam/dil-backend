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
        PhysicalDescription: auction.PhysicalDescription,
        FinalPrice: auction.FinalPrice);

    public static AuctionDeliveryDto? ToDeliveryDto(
        AuctionDelivery? delivery,
        bool canRateSeller = false,
        bool hasRatedSeller = false) =>
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
                DisputeEvidenceUrl: delivery.DisputeEvidenceUrl,
                AutoConfirmAt: delivery.Status is DeliveryStatus.InTransit && delivery.ShippedAt.HasValue
                    ? delivery.ShippedAt.Value.AddHours(AuctionDeliveryRules.AutoConfirmHours)
                    : null,
                IsPostPayoutDispute: delivery.IsPostPayoutDispute,
                DisputeResolution: delivery.DisputeResolution,
                DisputeResolvedAt: delivery.DisputeResolvedAt,
                DisputeResolvedNote: delivery.DisputeResolvedNote,
                CanRateSeller: canRateSeller,
                HasRatedSeller: hasRatedSeller);

    public static IReadOnlyList<AuctionReceiptDto> BuildReceipts(
        Domain.Entities.Auction.Auction auction,
        EscrowTransaction? escrow,
        AuctionDelivery? delivery)
    {
        var receipts = new List<AuctionReceiptDto>();
        var shortId = auction.Id.ToString()[..8].ToUpperInvariant();

        // Biên lai ký quỹ: câu mô tả phải theo TRẠNG THÁI HIỆN TẠI của ký quỹ. Trước đây luôn
        // ghi "đang tạm giữ" kể cả khi tiền đã chuyển cho người bán hoặc đã hoàn cho người mua.
        if (escrow is not null && auction.WinnerId.HasValue)
        {
            var depositNote = escrow.Status switch
            {
                EscrowStatus.Released =>
                    $"Khoản {escrow.Amount:N0} VND của phiên {auction.Id} đã được quyết toán: chuyển cho người bán {escrow.ReleasedAmount:N0} VND sau khi trừ phí nền tảng {escrow.FeeAmount:N0} VND.",
                EscrowStatus.Refunded =>
                    $"Khoản {escrow.Amount:N0} VND của phiên {auction.Id} đã được hoàn cho người mua.",
                EscrowStatus.Disputed =>
                    $"Khoản {escrow.Amount:N0} VND của phiên {auction.Id} đang bị đóng băng để chờ phân xử khiếu nại.",
                _ =>
                    $"Sàn Dillustration đang tạm giữ {escrow.Amount:N0} VND của phiên {auction.Id}. Đây là khoản giữ hộ, chưa chuyển cho người bán và chưa bị trừ phí nền tảng."
            };

            receipts.Add(new AuctionReceiptDto(
                ReceiptType: "EscrowDeposit",
                ReceiptNumber: $"REC-ESC-{shortId}",
                IssuedAt: escrow.CreatedAt,
                Amount: escrow.Amount,
                // Biên lai TẠM GIỮ chưa trừ phí — phí nền tảng chỉ phát sinh ở bước quyết toán.
                // Hiển thị phí ở đây khiến người dùng tưởng bị trừ tiền hai lần.
                FeeAmount: null,
                NetAmount: null,
                Title: "Biên lai Tạm giữ Tiền Ký quỹ Đấu giá",
                ProofImageUrl: null,
                Description: depositNote,
                PayerId: escrow.PayerId,
                PayeeId: escrow.PayeeId));
        }

        // Biên lai HOÀN TIỀN: trước đây không sinh, nên người mua đã được hoàn tiền chỉ thấy
        // đúng một biên nhận "đang tạm giữ" — vừa sai trạng thái vừa thiếu chứng từ.
        if (escrow is not null && escrow.Status == EscrowStatus.Refunded && escrow.RefundedAmount > 0m)
        {
            receipts.Add(new AuctionReceiptDto(
                ReceiptType: "EscrowRefund",
                ReceiptNumber: $"REC-RFD-{shortId}",
                IssuedAt: escrow.RefundedAt ?? escrow.UpdatedAt ?? escrow.CreatedAt,
                Amount: escrow.RefundedAmount,
                FeeAmount: null,
                NetAmount: escrow.RefundedAmount,
                Title: "Biên lai Hoàn tiền Ký quỹ",
                ProofImageUrl: null,
                Description: $"Đã hoàn {escrow.RefundedAmount:N0} VND về ví người mua cho phiên {auction.Id}.",
                PayerId: escrow.PayeeId,
                PayeeId: escrow.PayerId));
        }

        // Phiếu gửi bưu cục: sinh khi đơn ĐÃ được gửi và có BẤT KỲ bằng chứng nào (ảnh biên lai,
        // ảnh kiện hàng hoặc link tra cứu). Trước đây chỉ xét ảnh biên lai, nên người bán gửi
        // bằng link tra cứu/ảnh kiện hàng (đều hợp lệ theo API) thì không có biên nhận nào.
        var hasShippingEvidence = delivery is not null
                                  && (!string.IsNullOrWhiteSpace(delivery.ShippingReceiptUrl)
                                      || !string.IsNullOrWhiteSpace(delivery.PackageImageUrl)
                                      || !string.IsNullOrWhiteSpace(delivery.TrackingUrl));

        if (hasShippingEvidence && delivery is not null)
        {
            var proofImage = !string.IsNullOrWhiteSpace(delivery.ShippingReceiptUrl)
                ? delivery.ShippingReceiptUrl
                : delivery.PackageImageUrl;

            var evidenceNote = !string.IsNullOrWhiteSpace(delivery.TrackingUrl)
                ? $" Link tra cứu: {delivery.TrackingUrl}."
                : string.Empty;

            receipts.Add(new AuctionReceiptDto(
                ReceiptType: "CarrierShipping",
                ReceiptNumber: $"SHIP-{(string.IsNullOrWhiteSpace(delivery.TrackingCode) ? shortId : delivery.TrackingCode)}",
                IssuedAt: delivery.ShippedAt ?? delivery.CreatedAt,
                Amount: 0m,
                FeeAmount: null,
                NetAmount: null,
                Title: $"Phiếu gửi bưu cục ({delivery.CarrierName ?? "Vận chuyển"})",
                ProofImageUrl: proofImage,
                Description: $"Mã vận đơn: {delivery.TrackingCode}. Kiện hàng đã bàn giao cho bưu cục vận chuyển.{evidenceNote}",
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
                // Số tiền của biên nhận quyết toán là số TIỀN NGƯỜI BÁN THỰC NHẬN (net), không
                // phải giá chốt. Trước đây để Amount = escrow.Amount nên biên nhận ký quỹ và
                // biên nhận quyết toán cùng bằng giá chốt — cộng lại thành gấp đôi giá trị.
                Amount: escrow.ReleasedAmount,
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
