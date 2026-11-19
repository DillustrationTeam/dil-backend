using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using INotificationPublisher = ArtCommission.Application.Notifications.Common.INotificationPublisher;

namespace ArtCommission.Application.Auction.Commands.Delivery;

/// <summary>
/// Người thắng phiên có bản vật lý quyết định NHẬN TRANH ĐIỆN TỬ NGAY, chấp nhận bỏ bước
/// giao nhận bưu cục. Dùng khi người mua không muốn chờ bưu phẩm hoặc không còn nhu cầu
/// nhận bản gốc vật lý.
///
/// HỆ QUẢ TÀI CHÍNH — phải nói rõ cho người dùng trước khi gọi:
///   Tiền ký quỹ được giải ngân NGAY cho người bán và phiên đóng lại. Sau bước này sàn
///   không còn giữ tiền, nên nếu bưu phẩm (nếu vẫn trên đường) đến muộn hoặc hư hại thì
///   không còn quỹ ký quỹ để bồi hoàn.
///
/// Vì vậy command này chỉ được gọi sau khi FE đã hiển thị hộp thoại cảnh báo và người
/// dùng bấm đồng ý.
/// </summary>
public record AcceptDigitalDeliveryCommand(
    Guid UserId,
    Guid AuctionId
) : IRequest<(bool Success, AuctionDeliveryDto? Data, string[] Errors)>;

public class AcceptDigitalDeliveryCommandHandler
    : IRequestHandler<AcceptDigitalDeliveryCommand, (bool, AuctionDeliveryDto?, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly IAuctionSettlementService _settlementService;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<AcceptDigitalDeliveryCommandHandler> _logger;

    public AcceptDigitalDeliveryCommandHandler(
        IApplicationDbContext db,
        IAuctionSettlementService settlementService,
        INotificationPublisher notifications,
        ILogger<AcceptDigitalDeliveryCommandHandler> logger)
    {
        _db = db;
        _settlementService = settlementService;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<(bool, AuctionDeliveryDto?, string[])> Handle(
        AcceptDigitalDeliveryCommand request,
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
            return (false, null, ["Chỉ người thắng phiên mới có quyền chọn nhận tranh điện tử."]);
        }

        var delivery = await _db.Set<AuctionDelivery>()
            .FirstOrDefaultAsync(d => d.AuctionId == auction.Id && !d.IsDeleted, cancellationToken);

        // Vài phiên cũ được đánh dấu AwaitingPhysical nhưng chưa từng tạo dòng giao nhận
        // (dữ liệu seed / chỉnh tay). Vẫn phải cho người thắng nhận bản điện tử, nếu không
        // họ bị kẹt: không có bưu phẩm để chờ mà cũng không tải được tranh.
        if (delivery is null
            && auction.Status is not (AuctionStatus.AwaitingPhysical or AuctionStatus.InTransit))
        {
            return (false, null, ["Phiên này không có bước giao nhận tranh vật lý."]);
        }

        // Đã giao xong rồi thì không còn gì để chuyển đổi — trả về nguyên trạng.
        if (auction.Status is AuctionStatus.Completed
            || delivery?.Status is DeliveryStatus.Delivered)
        {
            return (true, AuctionMapper.ToDeliveryDto(delivery), []);
        }

        if (delivery?.Status is DeliveryStatus.Disputed)
        {
            return (false, null,
                ["Đơn đang khiếu nại, không thể chuyển sang nhận tranh điện tử. Vui lòng chờ Admin xử lý."]);
        }

        // Phiên/đơn đã khép lại thì không chuyển đổi hình thức nhận nữa. Thiếu guard này
        // thì sau khi phiên bị huỷ và tiền đã hoàn cho người mua, winner vẫn gọi được và
        // ép giải ngân cho người bán (trước đây hàm giải ngân không chặn trạng thái Refunded).
        if (auction.Status is AuctionStatus.Cancelled or AuctionStatus.Expired)
        {
            return (false, null,
                [$"Phiên đã ở trạng thái {AuctionMapper.DescribeStatus(auction.Status)} nên không chọn lại hình thức nhận được nữa."]);
        }

        if (delivery?.Status is DeliveryStatus.Cancelled or DeliveryStatus.Resolved)
        {
            return (false, null,
                [$"Đơn giao nhận đang ở trạng thái {delivery.Status} nên không chuyển sang nhận tranh điện tử được."]);
        }

        var now = DateTimeOffset.UtcNow;

        // KHÔNG huỷ đơn vận chuyển. Bưu phẩm vẫn đang trên đường giao tới người mua —
        // chỉ quỹ ký quỹ là được giải ngân sớm, nên từ đây người mua mất quyền khiếu nại
        // bồi hoàn. Vì vậy giữ nguyên trạng thái bưu kiện (AwaitingShipment / InTransit),
        // chỉ ghi nhận thời điểm cập nhật.
        if (delivery is not null)
        {
            delivery.UpdatedAt = now;
        }

        var (releaseSuccess, errors) = await _settlementService.ReleaseEscrowToSellerAsync(auction, cancellationToken);
        if (!releaseSuccess)
        {
            return (false, null, errors);
        }

        auction.RequiresPhysicalDelivery = false;
        auction.Status = AuctionStatus.Completed;
        auction.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Người mua {BuyerId} chọn nhận tranh điện tử ngay cho phiên {AuctionId}; ký quỹ đã giải ngân.",
            request.UserId, auction.Id);

        try
        {
            await _notifications.PublishAsync(
                userId: auction.SellerId,
                notificationType: NotificationType.AuctionLifecycle,
                title: "Người mua đã nhận tranh điện tử",
                body: $"Người thắng phiên #{auction.Id.ToString()[..8].ToUpperInvariant()} đã chọn nhận tranh điện tử và không chờ bưu phẩm. Tiền bán tranh đã được cộng vào ví của bạn.",
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
