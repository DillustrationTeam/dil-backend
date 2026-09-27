using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// MediatR cũng có INotificationPublisher — alias để không lấy nhầm của thư viện.
using INotificationPublisher = ArtCommission.Application.Notifications.Common.INotificationPublisher;

namespace ArtCommission.Application.Auction.Commands.PlaceBid;

/// <summary>
/// UC32 — POST /api/v1/auctions/{auctionId}/bids
/// Đặt giá trong phiên. Hệ thống khoá tiền cọc của bidder trong CÙNG transaction ACID.
///
/// BA BẤT BIẾN PHẢI GIỮ (nếu vi phạm là mất tiền thật):
///   1. Tối đa MỘT bid Leading mỗi phiên — ép cả ở tầng DB bằng filtered unique index.
///   2. Tiền cọc chỉ được giữ khi bid đã ghi; giải phóng cọc CŨ trước khi giữ cọc MỚI
///      trong cùng transaction.
///   3. Mọi lượt bid thôi dẫn đầu đều phải được nhả cọc (Refunded) — không để tiền chết.
/// </summary>
public record PlaceBidCommand(
    Guid UserId,
    Guid AuctionId,
    decimal Amount,
    bool IsAuto = false,
    decimal? MaxAutoBid = null
) : IRequest<(bool Success, PlaceBidResultDto? Data, string[] Errors)>;

public class PlaceBidCommandValidator : AbstractValidator<PlaceBidCommand>
{
    public PlaceBidCommandValidator()
    {
        RuleFor(x => x.AuctionId).NotEmpty().WithMessage("Thiếu mã phiên đấu giá.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền đặt giá phải lớn hơn 0.");

        RuleFor(x => x.MaxAutoBid)
            .GreaterThanOrEqualTo(x => x.Amount)
            .When(x => x.IsAuto && x.MaxAutoBid.HasValue)
            .WithMessage("Trần giá tự động phải lớn hơn hoặc bằng giá đặt.");

        RuleFor(x => x.MaxAutoBid)
            .NotNull()
            .When(x => x.IsAuto)
            .WithMessage("Đặt giá tự động phải kèm trần giá.");
    }
}

public class PlaceBidCommandHandler
    : IRequestHandler<PlaceBidCommand, (bool, PlaceBidResultDto?, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly IAuctionMoneyService _moneyService;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<PlaceBidCommandHandler> _logger;

    public PlaceBidCommandHandler(
        IApplicationDbContext db,
        IAuctionMoneyService moneyService,
        INotificationPublisher notifications,
        ILogger<PlaceBidCommandHandler> logger)
    {
        _db = db;
        _moneyService = moneyService;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<(bool, PlaceBidResultDto?, string[])> Handle(
        PlaceBidCommand request,
        CancellationToken cancellationToken)
    {
        var validation = new PlaceBidCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Đọc phiên trong transaction. Trên SQL Server, EF dịch FirstOrDefaultAsync
        // thành SELECT thường (không khoá) — hai request song song có thể cùng đọc
        // CurrentPrice cũ. Để chặn, dùng giá trị đã ghi + filtered unique index
        // trên bid Leading làm chốt chặn cuối: request thứ hai sẽ đụng index và rollback.
        var auction = await _db.Auctions.FirstOrDefaultAsync(
            a => a.Id == request.AuctionId && !a.IsDeleted,
            cancellationToken);

        if (auction is null)
        {
            return (false, null, ["Không tìm thấy phiên đấu giá."]);
        }

        var now = DateTimeOffset.UtcNow;

        // Chuyển trạng thái muộn: phiên Active nhưng đã quá EndAt coi như Ended.
        if (auction.Status == AuctionStatus.Active && auction.EndAt <= now)
        {
            auction.Status = AuctionStatus.Ended;
            auction.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
        }

        if (auction.Status != AuctionStatus.Active)
        {
            return (false, null,
                [$"Phiên không nhận đặt giá (trạng thái: {AuctionMapper.DescribeStatus(auction.Status)})."]);
        }

        if (auction.StartAt > now)
        {
            return (false, null, ["Phiên chưa bắt đầu."]);
        }

        // Người bán tự đẩy giá phiên của mình là gian lận — chặn ở server, không chỉ ở UI.
        if (auction.SellerId == request.UserId)
        {
            return (false, null, ["Người bán không thể tự đặt giá cho phiên của mình."]);
        }

        var minimumBid = auction.BidCount == 0
            ? auction.StartPrice
            : auction.CurrentPrice + auction.BidStep;

        if (request.Amount < minimumBid)
        {
            return (false, null,
                [$"Giá đặt tối thiểu là {minimumBid:N0} VND."]);
        }

        // Giá sàn bí mật: chặn bid thấp hơn ngay từ đầu để tránh phiên kết thúc mà không bán được.
        //
        // LỖI ĐÃ SỬA: message cũ in thẳng "phải đạt tối thiểu {ReservePrice} VND theo giá sàn"
        // cho MỌI bidder. Như giá sàn là thông tin bí mật (xem Auction.ReservePrice và
        // GetAuctionByIdQuery — endpoint chi tiết còn che trường này), in ra ở đây thì
        // bất kỳ ai cũng dò được chính xác giá sàn bằng cách bid thử vài lần.
        // Nay chỉ nói bid chưa đủ điều kiện, KHÔNG kèm con số.
        if (auction.ReservePrice.HasValue && request.Amount < auction.ReservePrice.Value)
        {
            return (false, null,
                ["Giá đặt chưa đạt mức tối thiểu mà người bán yêu cầu cho phiên này."]);
        }

        var previousLeading = await _db.Bids
            .Where(b => b.AuctionId == auction.Id && b.Status == BidStatus.Leading && !b.IsDeleted)
            .ToListAsync(cancellationToken);

        var previousLeaderId = previousLeading.FirstOrDefault()?.BidderId;

        // Tiền cọc = số tiền đặt giá (khớp hợp đồng API: holdAmount = amount).
        var holdAmount = request.Amount;

        // Bước 1: nhả cọc của (các) bid đang dẫn đầu và đánh dấu Outbid.
        foreach (var oldBid in previousLeading)
        {
            oldBid.Status = BidStatus.Outbid;
            oldBid.UpdatedAt = now;
            await _moneyService.RefundDepositAsync(
                oldBid, "Bị đè giá bởi lượt đặt giá cao hơn.", cancellationToken);
        }

        // LƯU NGAY trước khi thêm bid mới: filtered unique index chỉ cho một dòng
        // Status = 'Leading', nên dòng cũ phải rời trạng thái Leading trước.
        await _db.SaveChangesAsync(cancellationToken);

        // Bước 2: dựng bid TRƯỚC khi giữ tiền, để lấy được Bid.Id làm RefId cho dòng
        // sổ cái. BaseEntity sinh Id ngay khi khởi tạo nên không cần SaveChanges trung gian.
        // Bắt buộc dùng Bid.Id: RefId = Auction.Id sẽ đụng unique index
        // UX_WalletTransaction_Ref ngay ở lượt bid thứ hai của cùng phiên.
        var bid = new Bid
        {
            AuctionId = auction.Id,
            BidderId = request.UserId,
            Amount = request.Amount,
            HoldAmount = holdAmount,
            Status = BidStatus.Leading,
            // Chưa giữ được tiền thì chưa được đánh dấu Held — nếu giữ tiền thất bại
            // bên dưới, bản ghi không tồn tại nên HoldStatus cũng không quan trọng.
            HoldStatus = HoldStatus.None,
            IsAuto = request.IsAuto,
            MaxAutoBid = request.IsAuto ? request.MaxAutoBid : null,
            PlacedAt = now
        };

        var (holdResult, _) = await _moneyService.HoldDepositAsync(
            request.UserId, bid.Id, holdAmount, cancellationToken);

        if (!holdResult.Success)
        {
            await tx.RollbackAsync(cancellationToken);
            return (false, null, holdResult.Errors);
        }

        bid.HoldStatus = HoldStatus.Held;

        _db.Bids.Add(bid);

        auction.CurrentPrice = request.Amount;
        auction.BidCount += 1;
        auction.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Đặt giá thành công: auctionId={AuctionId}, bidId={BidId}, bidderId={BidderId}, amount={Amount}, hold={Hold}.",
            auction.Id, bid.Id, request.UserId, request.Amount, holdAmount);

        // Thông báo cho người vừa bị đè giá. Phát SAU commit: nếu phát trước mà
        // transaction rollback thì người dùng nhận cảnh báo cho việc không xảy ra.
        if (previousLeaderId.HasValue && previousLeaderId.Value != request.UserId)
        {
            try
            {
                await _notifications.PublishAsync(
                    previousLeaderId.Value,
                    NotificationType.OutbidAlert,
                    "Bạn vừa bị đè giá",
                    $"Có người đặt {request.Amount:N0} VND cho phiên bạn đang dẫn đầu. Tiền cọc đã được hoàn về ví.",
                    nameof(Domain.Entities.Auction.Auction),
                    auction.Id,
                    NotificationChannel.InApp,
                    $"Outbid:{auction.Id}:{previousLeaderId.Value}:{bid.Id}",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                // Không được để lỗi thông báo làm hỏng nghiệp vụ đã commit thành công.
                _logger.LogError(ex,
                    "Không phát được thông báo đè giá cho userId={UserId}, auctionId={AuctionId}.",
                    previousLeaderId.Value, auction.Id);
            }
        }

        var bidDto = AuctionMapper.ToDto(bid);
        return (true, new PlaceBidResultDto(
            bidDto,
            new WalletSnapshotDto(holdResult.Balance, holdResult.LockedBalance)), []);
    }
}
