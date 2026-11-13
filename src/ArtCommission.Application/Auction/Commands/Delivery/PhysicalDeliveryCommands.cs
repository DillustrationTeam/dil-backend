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

        // Đã chọn nhận bản điện tử (hoặc phiên đã đóng) thì không quay lại xin giao tranh
        // vật lý được nữa — ký quỹ đã giải ngân cho người bán, giao thêm hàng là vô nghĩa.
        if (auction.RequiresPhysicalDelivery == false
            || auction.Status is AuctionStatus.Settled or AuctionStatus.Completed)
        {
            return (false, null,
                ["Bạn đã chọn nhận bản điện tử nên không thể yêu cầu giao tranh vật lý nữa."]);
        }

        var now = DateTimeOffset.UtcNow;
        if (auction.PhysicalChoiceDeadline.HasValue && auction.PhysicalChoiceDeadline.Value < now)
        {
            return (false, null, ["Đã quá thời hạn 48 giờ để yêu cầu nhận tranh vật lý."]);
        }

        var delivery = await _db.Set<AuctionDelivery>()
            .FirstOrDefaultAsync(d => d.AuctionId == auction.Id && !d.IsDeleted, cancellationToken);

        // Đơn đã đi qua bước chọn hình thức (đang giao / đang khiếu nại / đã xử lý) thì
        // KHÔNG được ghi đè ngược về "chờ gửi hàng". Trước đây hàm này ép trạng thái về
        // AwaitingShipment vô điều kiện nên người mua có thể "rã đông" một đơn đang khiếu
        // nại, xoá dấu đóng băng ký quỹ và thay địa chỉ sau khi người bán đã gửi hàng.
        if (delivery is not null && delivery.Status != DeliveryStatus.AwaitingShipment)
        {
            return (false, null,
                [$"Đơn giao nhận đang ở trạng thái {delivery.Status} nên không nhập lại thông tin nhận hàng được."]);
        }

        if (auction.Status is AuctionStatus.Disputed
            or AuctionStatus.Cancelled
            or AuctionStatus.Expired
            or AuctionStatus.InTransit)
        {
            return (false, null,
                [$"Phiên đang ở trạng thái {AuctionMapper.DescribeStatus(auction.Status)} nên không nhận yêu cầu giao tranh vật lý."]);
        }

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

        // Phiên đã khép lại (huỷ / quá hạn / đã hoàn tất) thì không còn gì để chọn.
        // Thiếu guard này thì trên phiên ĐÃ HOÀN TIỀN cho người mua, winner vẫn bấm
        // "chỉ nhận file số" được và hàm giải ngân cộng tiền cho người bán lần thứ hai.
        if (auction.Status is AuctionStatus.Cancelled
            or AuctionStatus.Expired
            or AuctionStatus.Completed
            or AuctionStatus.Settled)
        {
            return (false,
                [$"Phiên đã ở trạng thái {AuctionMapper.DescribeStatus(auction.Status)} nên không chọn lại hình thức nhận được nữa."]);
        }

        var now = DateTimeOffset.UtcNow;

        var delivery = await _db.Set<AuctionDelivery>()
            .FirstOrDefaultAsync(d => d.AuctionId == auction.Id && !d.IsDeleted, cancellationToken);

        // Người bán đã gửi hàng rồi thì "bỏ tranh thật" không còn nghĩa: hàng đang trên
        // đường, giải ngân lúc này là tước mất quyền khiếu nại bồi hoàn của người mua.
        if (delivery?.Status is DeliveryStatus.InTransit)
        {
            return (false,
                ["Người bán đã gửi hàng. Hãy nhận hàng hoặc gửi khiếu nại nếu bưu phẩm có vấn đề."]);
        }

        if (delivery?.Status is DeliveryStatus.Disputed or DeliveryStatus.Resolved or DeliveryStatus.Cancelled)
        {
            return (false,
                [$"Đơn giao nhận đang ở trạng thái {delivery.Status} nên không thể chuyển sang nhận file số."]);
        }

        auction.RequiresPhysicalDelivery = false;
        auction.UpdatedAt = now;

        // Nhả tiền ngay cho người bán và cấp quyền tải file số
        var (releaseSuccess, errors) = await _settlementService.ReleaseEscrowToSellerAsync(auction, cancellationToken);
        if (!releaseSuccess)
        {
            return (false, errors);
        }

        // KHÔNG set lại Status ở đây: ReleaseEscrowToSellerAsync đã đặt Completed (đúng
        // trạng thái kết thúc giao dịch). Trước đây gán đè thành Settled khiến phiên đi
        // ngược trạng thái và người mua mất quyền đánh giá người bán.
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

        // Bắt buộc có ít nhất MỘT bằng chứng gửi hàng để người mua đối soát được:
        // ảnh biên lai bưu cục, ảnh kiện hàng đã đóng gói, hoặc link tra cứu hành trình.
        // (Kiểm tra sau bước phân quyền để lỗi 400 trả về đúng lý do cho từng trường hợp.)
        if (string.IsNullOrWhiteSpace(request.ShippingReceiptUrl)
            && string.IsNullOrWhiteSpace(request.PackageImageUrl)
            && string.IsNullOrWhiteSpace(request.TrackingUrl))
        {
            return (false, null,
                ["Vui lòng cung cấp ít nhất một bằng chứng gửi hàng: ảnh biên lai bưu cục, ảnh kiện hàng hoặc link tra cứu hành trình."]);
        }

        var delivery = await _db.Set<AuctionDelivery>()
            .FirstOrDefaultAsync(d => d.AuctionId == auction.Id && !d.IsDeleted, cancellationToken);

        if (delivery is null)
        {
            return (false, null, ["Người mua chưa yêu cầu giao tranh vật lý hoặc chưa có thông tin nhận hàng."]);
        }

        // Người mua đã nhận bản điện tử (phiên Completed, ký quỹ giải ngân sớm) thì NGƯỜI BÁN
        // VẪN PHẢI giao tranh vật lý — chỉ khác là không còn quỹ bồi hoàn. Vì vậy chỉ chặn ship
        // khi đơn đã giao xong / đã huỷ / đang khiếu nại, hoặc phiên bị huỷ - hết hạn.
        if (delivery.Status is DeliveryStatus.Delivered or DeliveryStatus.Cancelled
            or DeliveryStatus.Disputed or DeliveryStatus.Resolved)
        {
            return (false, null,
                ["Đơn giao nhận đã kết thúc hoặc đang khiếu nại, không cập nhật vận đơn được nữa."]);
        }

        if (auction.Status is AuctionStatus.Cancelled or AuctionStatus.Expired or AuctionStatus.Disputed)
        {
            return (false, null,
                ["Phiên đã huỷ, hết hạn hoặc đang khiếu nại nên không cập nhật vận đơn được nữa."]);
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

        // Người mua có thể đã nhận được tranh dù người bán chưa kịp cập nhật vận đơn,
        // nên cho xác nhận từ cả AwaitingShipment. Chỉ chặn khi đơn đã chốt/huỷ.
        if (delivery.Status is not (DeliveryStatus.AwaitingShipment or DeliveryStatus.InTransit))
        {
            return (false, null,
                ["Đơn giao nhận đã ở trạng thái kết thúc, không thể xác nhận nhận hàng."]);
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

        // Phiên đã huỷ / hết hạn thì không còn gì để khiếu nại.
        if (auction.Status is AuctionStatus.Cancelled or AuctionStatus.Expired)
        {
            return (false, null,
                ["Phiên đã huỷ hoặc hết hạn nên không thể mở khiếu nại giao nhận."]);
        }

        var delivery = await _db.Set<AuctionDelivery>()
            .FirstOrDefaultAsync(d => d.AuctionId == auction.Id && !d.IsDeleted, cancellationToken);

        if (delivery is null)
        {
            return (false, null, ["Không tìm thấy thông tin giao nhận của phiên."]);
        }

        if (delivery.Status == DeliveryStatus.Disputed)
        {
            return (false, null, ["Khiếu nại của phiên này đã được ghi nhận và đang chờ phân xử."]);
        }

        // Mỗi đơn chỉ mở một hồ sơ khiếu nại; Admin đã đóng thì chỉ còn quyền đánh giá người bán.
        if (delivery.Status == DeliveryStatus.Resolved)
        {
            return (false, null,
                ["Khiếu nại của phiên này đã được Ban quản trị xử lý xong. Bạn có thể đánh giá người bán."]);
        }

        var escrow = await _db.EscrowTransactions
            .FirstOrDefaultAsync(e => e.AuctionId == auction.Id && !e.IsDeleted, cancellationToken);

        // Ký quỹ đã giải ngân (người mua nhận bản điện tử hoặc Admin đã bác khiếu nại trước đó)
        // thì vẫn PHẢI ghi nhận phản ánh của người mua: tranh vật lý vẫn đang trên đường tới,
        // hàng có thể vỡ hỏng. Chỉ khác là không còn quỹ để đóng băng hay hoàn lại — khiếu nại
        // được đánh dấu IsPostPayoutDispute để Admin biết mà đối soát và để người mua dùng
        // quyền đánh giá người bán.
        var isPostPayout = auction.Status == AuctionStatus.Completed
                           || escrow?.Status == EscrowStatus.Released;

        var now = DateTimeOffset.UtcNow;
        delivery.Status = DeliveryStatus.Disputed;
        delivery.DisputeReason = request.DisputeReason.Trim();
        delivery.DisputeEvidenceUrl = request.DisputeEvidenceUrl?.Trim();
        delivery.IsPostPayoutDispute = isPostPayout;
        delivery.DisputeResolution = null;
        delivery.DisputeResolvedAt = null;
        delivery.DisputeResolvedNote = null;
        delivery.UpdatedAt = now;

        // Phiên phải phản ánh đúng là đang tranh chấp, nếu không dashboard/Admin vẫn thấy
        // "đang giao" và các guard phía sau (ship/confirm) không nhận ra đang bị đóng băng.
        // Riêng phiên đã Completed thì giữ nguyên: tiền đã về ví người bán, đổi trạng thái
        // phiên chỉ làm sai lệch sổ sách chứ không thu hồi được đồng nào.
        if (!isPostPayout)
        {
            auction.Status = AuctionStatus.Disputed;
            auction.UpdatedAt = now;
        }

        if (escrow is not null && escrow.Status != EscrowStatus.Released)
        {
            escrow.Status = EscrowStatus.Disputed;
            escrow.Note = $"Khiếu nại: {request.DisputeReason.Trim()}";
            escrow.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Người mua {BuyerId} đã khiếu nại giao nhận phiên {AuctionId} (sau giải ngân: {PostPayout}): {Reason}",
            request.UserId, auction.Id, isPostPayout, request.DisputeReason);

        var shortId = auction.Id.ToString()[..8].ToUpperInvariant();

        try
        {
            await _notifications.PublishAsync(
                userId: auction.SellerId,
                notificationType: NotificationType.DisputeStatusChanged,
                title: isPostPayout
                    ? "Người mua phản ánh đơn hàng sau khi đã giải ngân"
                    : "Khiếu nại đơn giao tranh",
                body: isPostPayout
                    ? $"Người mua báo vấn đề với tranh vật lý phiên #{shortId}: {request.DisputeReason.Trim()}. Tiền bán tranh đã về ví bạn nên sàn không thể tự động hoàn lại — Ban quản trị sẽ đối soát bưu cục và điểm uy tín của bạn có thể bị ảnh hưởng."
                    : $"Người mua đã gửi khiếu nại về phiên #{shortId}: {request.DisputeReason.Trim()}. Ký quỹ đang bị đóng băng, Ban quản trị sẽ đối soát bưu cục.",
                refType: "Auction",
                refId: auction.Id,
                cancellationToken: cancellationToken);

            // Người mua cũng phải nhận thông báo, để biết tiền đã giải ngân thì không thu hồi
            // được nữa và mình còn quyền đánh giá người bán.
            await _notifications.PublishAsync(
                userId: request.UserId,
                notificationType: NotificationType.DisputeStatusChanged,
                title: isPostPayout
                    ? "Đã ghi nhận phản ánh — tiền đã giải ngân cho người bán"
                    : "Đã ghi nhận khiếu nại giao nhận",
                body: isPostPayout
                    ? $"Phản ánh của bạn về phiên #{shortId} đã được ghi nhận. Ký quỹ đã giải ngân cho người bán trước đó nên không thể hoàn tiền được nữa; Ban quản trị sẽ đối soát bưu cục. Bạn có thể đánh giá người bán để cảnh báo cộng đồng."
                    : $"Khiếu nại của bạn về phiên #{shortId} đã được ghi nhận và ký quỹ đã được tạm đóng băng để Ban quản trị đối soát.",
                refType: "Auction",
                refId: auction.Id,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi gửi thông báo khiếu nại phiên {AuctionId}", auction.Id);
        }

        return (true, AuctionMapper.ToDeliveryDto(delivery), []);
    }
}
