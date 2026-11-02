using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// Alias tránh trùng tên với MediatR
using INotificationPublisher = ArtCommission.Application.Notifications.Common.INotificationPublisher;

namespace ArtCommission.Application.Auction.Commands.Delivery;

// =====================================================================
// 1. Người thắng yêu cầu nhận tranh vật lý và gửi thông tin địa chỉ
// =====================================================================

public record RequestPhysicalDeliveryCommand(
    Guid UserId,
    Guid AuctionId,
    string RecipientName,
    string RecipientPhone,
    string ShippingAddress,
    string? NoteToSeller = null
) : IRequest<(bool Success, AuctionDeliveryDto? Data, string[] Errors)>;

public class RequestPhysicalDeliveryCommandHandler
    : IRequestHandler<RequestPhysicalDeliveryCommand, (bool, AuctionDeliveryDto?, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<RequestPhysicalDeliveryCommandHandler> _logger;

    public RequestPhysicalDeliveryCommandHandler(
        IApplicationDbContext db,
        INotificationPublisher notifications,
        ILogger<RequestPhysicalDeliveryCommandHandler> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<(bool, AuctionDeliveryDto?, string[])> Handle(
        RequestPhysicalDeliveryCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RecipientName))
        {
            return (false, null, ["Họ tên người nhận không được để trống."]);
        }
        if (string.IsNullOrWhiteSpace(request.RecipientPhone))
        {
            return (false, null, ["Số điện thoại người nhận không được để trống."]);
        }
        if (string.IsNullOrWhiteSpace(request.ShippingAddress))
        {
            return (false, null, ["Địa chỉ nhận hàng không được để trống."]);
        }

        var auction = await _db.Auctions
            .FirstOrDefaultAsync(a => a.Id == request.AuctionId && !a.IsDeleted, cancellationToken);

        if (auction is null)
        {
            return (false, null, ["Không tìm thấy phiên đấu giá."]);
        }

        if (auction.WinnerId != request.UserId)
        {
            return (false, null, ["Chỉ người thắng phiên mới có quyền yêu cầu nhận tranh vật lý."]);
        }

        if (!auction.HasPhysicalCopy)
        {
            return (false, null, ["Phiên đấu giá này không hỗ trợ bàn giao bản tranh vật lý."]);
        }

        var now = DateTimeOffset.UtcNow;
        if (auction.PhysicalChoiceDeadline.HasValue && auction.PhysicalChoiceDeadline.Value < now)
        {
            return (false, null, ["Đã quá thời hạn 48 giờ để yêu cầu nhận tranh vật lý."]);
        }

        var delivery = await _db.Set<AuctionDelivery>()
            .FirstOrDefaultAsync(d => d.AuctionId == auction.Id && !d.IsDeleted, cancellationToken);

        if (delivery is null)
        {
            delivery = new AuctionDelivery
            {
                AuctionId = auction.Id,
                RecipientId = request.UserId,
                RecipientName = request.RecipientName.Trim(),
                RecipientPhone = request.RecipientPhone.Trim(),
                ShippingAddress = request.ShippingAddress.Trim(),
                NoteToSeller = request.NoteToSeller?.Trim(),
                Status = DeliveryStatus.AwaitingShipment
            };
            _db.Set<AuctionDelivery>().Add(delivery);
        }
        else
        {
            delivery.RecipientName = request.RecipientName.Trim();
            delivery.RecipientPhone = request.RecipientPhone.Trim();
            delivery.ShippingAddress = request.ShippingAddress.Trim();
            delivery.NoteToSeller = request.NoteToSeller?.Trim();
            delivery.Status = DeliveryStatus.AwaitingShipment;
            delivery.UpdatedAt = now;
        }

        auction.RequiresPhysicalDelivery = true;
        auction.Status = AuctionStatus.AwaitingPhysical;
        auction.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Người thắng {WinnerId} đã yêu cầu nhận tranh vật lý phiên {AuctionId}.",
            request.UserId, auction.Id);

        // Báo cho người bán biết có yêu cầu giao tranh vật lý
        try
        {
            await _notifications.PublishAsync(
                userId: auction.SellerId,
                notificationType: NotificationType.AuctionLifecycle,
                title: "Yêu cầu giao tranh vật lý",
                body: $"Người thắng phiên đấu giá #{auction.Id.ToString()[..8].ToUpperInvariant()} đã yêu cầu nhận tranh vật lý. Vui lòng đóng gói và gửi hàng trong vòng 3 ngày.",
                refType: "Auction",
                refId: auction.Id,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi gửi thông báo cho Seller {SellerId}", auction.SellerId);
        }

        return (true, AuctionMapper.ToDeliveryDto(delivery), []);
    }
}

// =====================================================================
// 2. Người thắng từ chối nhận tranh vật lý (chỉ nhận file số)
// =====================================================================

public record DeclinePhysicalDeliveryCommand(
    Guid UserId,
    Guid AuctionId
) : IRequest<(bool Success, string[] Errors)>;

public class DeclinePhysicalDeliveryCommandHandler
    : IRequestHandler<DeclinePhysicalDeliveryCommand, (bool, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly IAuctionSettlementService _settlementService;
    private readonly ILogger<DeclinePhysicalDeliveryCommandHandler> _logger;

    public DeclinePhysicalDeliveryCommandHandler(
        IApplicationDbContext db,
        IAuctionSettlementService settlementService,
        ILogger<DeclinePhysicalDeliveryCommandHandler> logger)
    {
        _db = db;
        _settlementService = settlementService;
        _logger = logger;
    }

    public async Task<(bool, string[])> Handle(
        DeclinePhysicalDeliveryCommand request,
        CancellationToken cancellationToken)
    {
        var auction = await _db.Auctions
            .FirstOrDefaultAsync(a => a.Id == request.AuctionId && !a.IsDeleted, cancellationToken);

        if (auction is null)
        {
            return (false, ["Không tìm thấy phiên đấu giá."]);
        }

        if (auction.WinnerId != request.UserId)
        {
            return (false, ["Chỉ người thắng phiên mới có quyền chọn hình thức nhận tranh."]);
        }

        var now = DateTimeOffset.UtcNow;
        auction.RequiresPhysicalDelivery = false;
        auction.UpdatedAt = now;

        // Nhả tiền ngay cho người bán và cấp quyền tải file số
        var (releaseSuccess, errors) = await _settlementService.ReleaseEscrowToSellerAsync(auction, cancellationToken);
        if (!releaseSuccess)
        {
            return (false, errors);
        }

        auction.Status = AuctionStatus.Settled;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Người thắng {WinnerId} chọn chỉ nhận file số phiên {AuctionId}. Đã giải ngân cho Seller.",
            request.UserId, auction.Id);

        return (true, []);
    }
}

// =====================================================================
// 3. Người bán cập nhật mã vận đơn & ảnh biên lai bưu cục
// =====================================================================

public record UpdateShippingInfoCommand(
    Guid UserId,
    Guid AuctionId,
    string CarrierName,
    string TrackingCode,
    string? TrackingUrl = null,
    string? ShippingReceiptUrl = null,
    string? PackageImageUrl = null
) : IRequest<(bool Success, AuctionDeliveryDto? Data, string[] Errors)>;

public class UpdateShippingInfoCommandHandler
    : IRequestHandler<UpdateShippingInfoCommand, (bool, AuctionDeliveryDto?, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<UpdateShippingInfoCommandHandler> _logger;

    public UpdateShippingInfoCommandHandler(
        IApplicationDbContext db,
        INotificationPublisher notifications,
        ILogger<UpdateShippingInfoCommandHandler> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<(bool, AuctionDeliveryDto?, string[])> Handle(
        UpdateShippingInfoCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CarrierName))
        {
            return (false, null, ["Tên đơn vị vận chuyển không được để trống."]);
        }
        if (string.IsNullOrWhiteSpace(request.TrackingCode))
        {
            return (false, null, ["Mã vận đơn bưu cục không được để trống."]);
        }

        var auction = await _db.Auctions
            .FirstOrDefaultAsync(a => a.Id == request.AuctionId && !a.IsDeleted, cancellationToken);

        if (auction is null)
        {
            return (false, null, ["Không tìm thấy phiên đấu giá."]);
        }

        if (auction.SellerId != request.UserId)
        {
            return (false, null, ["Chỉ người bán của phiên mới có quyền cập nhật thông tin vận chuyển."]);
        }

        var delivery = await _db.Set<AuctionDelivery>()
            .FirstOrDefaultAsync(d => d.AuctionId == auction.Id && !d.IsDeleted, cancellationToken);

        if (delivery is null)
        {
            return (false, null, ["Người mua chưa yêu cầu giao tranh vật lý hoặc chưa có thông tin nhận hàng."]);
        }

        var now = DateTimeOffset.UtcNow;
        delivery.CarrierName = request.CarrierName.Trim();
        delivery.TrackingCode = request.TrackingCode.Trim();
        delivery.TrackingUrl = request.TrackingUrl?.Trim();
        delivery.ShippingReceiptUrl = request.ShippingReceiptUrl?.Trim();
        delivery.PackageImageUrl = request.PackageImageUrl?.Trim();
        delivery.ShippedAt = now;
        delivery.Status = DeliveryStatus.InTransit;
        delivery.UpdatedAt = now;

        auction.Status = AuctionStatus.InTransit;
        auction.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Người bán {SellerId} đã gửi tranh phiên {AuctionId}. Hãng: {Carrier}, Mã: {Tracking}.",
            request.UserId, auction.Id, delivery.CarrierName, delivery.TrackingCode);

        // Báo cho người mua biết tranh đã được gửi
        if (auction.WinnerId.HasValue)
        {
            try
            {
                await _notifications.PublishAsync(
                    userId: auction.WinnerId.Value,
                    notificationType: NotificationType.AuctionLifecycle,
                    title: "Tranh của bạn đang được giao",
                    body: $"Tác phẩm từ phiên #{auction.Id.ToString()[..8].ToUpperInvariant()} đã được gửi qua {delivery.CarrierName}. Mã vận đơn: {delivery.TrackingCode}.",
                    refType: "Auction",
                    refId: auction.Id,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi gửi thông báo cho Buyer {WinnerId}", auction.WinnerId);
            }
        }

        return (true, AuctionMapper.ToDeliveryDto(delivery), []);
    }
}

// =====================================================================
// 4. Người mua xác nhận đã nhận tranh và hài lòng (Nhả tiền cho Seller)
// =====================================================================

public record ConfirmPhysicalDeliveryCommand(
    Guid UserId,
    Guid AuctionId,
    string? DeliveryReceiptUrl = null
) : IRequest<(bool Success, AuctionDeliveryDto? Data, string[] Errors)>;

public class ConfirmPhysicalDeliveryCommandHandler
    : IRequestHandler<ConfirmPhysicalDeliveryCommand, (bool, AuctionDeliveryDto?, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly IAuctionSettlementService _settlementService;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<ConfirmPhysicalDeliveryCommandHandler> _logger;

    public ConfirmPhysicalDeliveryCommandHandler(
        IApplicationDbContext db,
        IAuctionSettlementService settlementService,
        INotificationPublisher notifications,
        ILogger<ConfirmPhysicalDeliveryCommandHandler> logger)
    {
        _db = db;
        _settlementService = settlementService;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<(bool, AuctionDeliveryDto?, string[])> Handle(
        ConfirmPhysicalDeliveryCommand request,
        CancellationToken cancellationToken)
    {
        var auction = await _db.Auctions
            .FirstOrDefaultAsync(a => a.Id == request.AuctionId && !a.IsDeleted, cancellationToken);

        if (auction is null)
        {
            return (false, null, ["Không tìm thấy phiên đấu giá."]);
        }

        if (auction.WinnerId != request.UserId)
        {
            return (false, null, ["Chỉ người thắng phiên mới có quyền xác nhận nhận hàng."]);
        }

        var delivery = await _db.Set<AuctionDelivery>()
            .FirstOrDefaultAsync(d => d.AuctionId == auction.Id && !d.IsDeleted, cancellationToken);

        if (delivery is null)
        {
            return (false, null, ["Không tìm thấy thông tin giao nhận của phiên."]);
        }

        if (delivery.Status == DeliveryStatus.Delivered && auction.Status == AuctionStatus.Completed)
        {
            return (true, AuctionMapper.ToDeliveryDto(delivery), []); // Idempotent
        }

        var now = DateTimeOffset.UtcNow;
        delivery.Status = DeliveryStatus.Delivered;
        delivery.DeliveredAt ??= now;
        delivery.ConfirmedAt = now;
        if (!string.IsNullOrWhiteSpace(request.DeliveryReceiptUrl))
        {
            delivery.DeliveryReceiptUrl = request.DeliveryReceiptUrl.Trim();
        }
        delivery.UpdatedAt = now;

        // Giải ngân tiền ký quỹ cho Người bán
        var (releaseSuccess, errors) = await _settlementService.ReleaseEscrowToSellerAsync(auction, cancellationToken);
        if (!releaseSuccess)
        {
            return (false, null, errors);
        }

        auction.Status = AuctionStatus.Completed;
        auction.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Người mua {BuyerId} đã xác nhận nhận tranh phiên {AuctionId}. Giao dịch hoàn tất thành công.",
            request.UserId, auction.Id);

        // Báo cho người bán biết tiền đã về ví
        try
        {
            await _notifications.PublishAsync(
                userId: auction.SellerId,
                notificationType: NotificationType.AuctionLifecycle,
                title: "Thanh toán đã được giải ngân",
                body: $"Người mua đã xác nhận nhận tranh vật lý phiên #{auction.Id.ToString()[..8].ToUpperInvariant()}. Tiền bán tranh đã được cộng vào ví của bạn!",
                refType: "Auction",
                refId: auction.Id,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi gửi thông báo giải ngân cho Seller {SellerId}", auction.SellerId);
        }

        return (true, AuctionMapper.ToDeliveryDto(delivery), []);
    }
}

// =====================================================================
// 5. Người mua khiếu nại (Chưa nhận được tranh hoặc tranh hư hại)
// =====================================================================

public record DisputePhysicalDeliveryCommand(
    Guid UserId,
    Guid AuctionId,
    string DisputeReason,
    string? DisputeEvidenceUrl = null
) : IRequest<(bool Success, AuctionDeliveryDto? Data, string[] Errors)>;

public class DisputePhysicalDeliveryCommandHandler
    : IRequestHandler<DisputePhysicalDeliveryCommand, (bool, AuctionDeliveryDto?, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<DisputePhysicalDeliveryCommandHandler> _logger;

    public DisputePhysicalDeliveryCommandHandler(
        IApplicationDbContext db,
        INotificationPublisher notifications,
        ILogger<DisputePhysicalDeliveryCommandHandler> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<(bool, AuctionDeliveryDto?, string[])> Handle(
        DisputePhysicalDeliveryCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DisputeReason))
        {
            return (false, null, ["Lý do khiếu nại không được để trống."]);
        }

        var auction = await _db.Auctions
            .FirstOrDefaultAsync(a => a.Id == request.AuctionId && !a.IsDeleted, cancellationToken);

        if (auction is null)
        {
            return (false, null, ["Không tìm thấy phiên đấu giá."]);
        }

        if (auction.WinnerId != request.UserId)
        {
            return (false, null, ["Chỉ người thắng phiên mới có quyền gửi khiếu nại."]);
        }

        var delivery = await _db.Set<AuctionDelivery>()
            .FirstOrDefaultAsync(d => d.AuctionId == auction.Id && !d.IsDeleted, cancellationToken);

        if (delivery is null)
        {
            return (false, null, ["Không tìm thấy thông tin giao nhận của phiên."]);
        }

        var now = DateTimeOffset.UtcNow;
        delivery.Status = DeliveryStatus.Disputed;
        delivery.DisputeReason = request.DisputeReason.Trim();
        delivery.DisputeEvidenceUrl = request.DisputeEvidenceUrl?.Trim();
        delivery.UpdatedAt = now;

        var escrow = await _db.EscrowTransactions
            .FirstOrDefaultAsync(e => e.AuctionId == auction.Id && !e.IsDeleted, cancellationToken);
        if (escrow is not null && escrow.Status != EscrowStatus.Released)
        {
            escrow.Status = EscrowStatus.Disputed;
            escrow.Note = $"Khiếu nại: {request.DisputeReason.Trim()}";
            escrow.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Người mua {BuyerId} đã khiếu nại giao nhận phiên {AuctionId}: {Reason}",
            request.UserId, auction.Id, request.DisputeReason);

        try
        {
            await _notifications.PublishAsync(
                userId: auction.SellerId,
                notificationType: NotificationType.AuctionLifecycle,
                title: "Khiếu nại đơn giao tranh",
                body: $"Người mua đã gửi khiếu nại về phiên #{auction.Id.ToString()[..8].ToUpperInvariant()}: {request.DisputeReason}. Ban quản trị sẽ đối soát bưu cục.",
                refType: "Auction",
                refId: auction.Id,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi gửi thông báo cho Seller {SellerId}", auction.SellerId);
        }

        return (true, AuctionMapper.ToDeliveryDto(delivery), []);
    }
}
