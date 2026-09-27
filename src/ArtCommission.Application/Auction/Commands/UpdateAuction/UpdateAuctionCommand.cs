using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Commands.UpdateAuction;

/// <summary>
/// UC32 — PUT /api/v1/auctions/{auctionId}
/// Người bán sửa phiên khi CHƯA có bid và phiên còn ở trạng thái Scheduled.
///
/// VÌ SAO siết hai điều kiện đó: sửa giá sau khi đã có người đặt giá là thay đổi
/// luật chơi giữa cuộc — người đã bid có quyền dựa vào con số họ nhìn thấy.
/// </summary>
public record UpdateAuctionCommand(
    Guid UserId,
    Guid AuctionId,
    decimal? StartPrice,
    decimal? ReservePrice,
    decimal? BidStep,
    decimal? BuyNowPrice,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt,
    bool ClearBuyNowPrice = false,
    bool ClearReservePrice = false
) : IRequest<(bool Success, AuctionDto? Data, string[] Errors)>;

public class UpdateAuctionCommandValidator : AbstractValidator<UpdateAuctionCommand>
{
    public UpdateAuctionCommandValidator()
    {
        RuleFor(x => x.AuctionId).NotEmpty();

        RuleFor(x => x.StartPrice)
            .GreaterThan(0).When(x => x.StartPrice.HasValue)
            .WithMessage("Giá khởi điểm phải lớn hơn 0.");

        RuleFor(x => x.BidStep)
            .GreaterThan(0).When(x => x.BidStep.HasValue)
            .WithMessage("Bước giá phải lớn hơn 0.");

        RuleFor(x => x.EndAt)
            .Must(end => end > DateTimeOffset.UtcNow)
            .When(x => x.EndAt.HasValue)
            .WithMessage("Thời điểm kết thúc mới phải ở tương lai.");
    }
}

public class UpdateAuctionCommandHandler
    : IRequestHandler<UpdateAuctionCommand, (bool, AuctionDto?, string[])>
{
    private readonly IApplicationDbContext _db;

    public UpdateAuctionCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, AuctionDto?, string[])> Handle(
        UpdateAuctionCommand request,
        CancellationToken cancellationToken)
    {
        var validation = new UpdateAuctionCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var auction = await _db.Auctions.FirstOrDefaultAsync(
            a => a.Id == request.AuctionId && !a.IsDeleted,
            cancellationToken);

        if (auction is null)
        {
            return (false, null, ["Không tìm thấy phiên đấu giá."]);
        }

        if (auction.SellerId != request.UserId)
        {
            return (false, null, ["Chỉ người bán mới được sửa phiên này."]);
        }

        if (auction.Status != AuctionStatus.Scheduled)
        {
            return (false, null,
                [$"Chỉ sửa được phiên đang ở trạng thái đã lên lịch (hiện tại: {AuctionMapper.DescribeStatus(auction.Status)})."]);
        }

        if (auction.BidCount > 0)
        {
            return (false, null, ["Phiên đã có lượt đặt giá nên không sửa được nữa."]);
        }

        var now = DateTimeOffset.UtcNow;

        // Kiểm tra chéo SAU khi ghép giá trị mới với giá trị đang có — nếu chỉ validate
        // từng trường riêng lẻ thì bộ giá trị ghép lại vẫn có thể vô lý.
        var newStartPrice = request.StartPrice ?? auction.StartPrice;
        var newBidStep = request.BidStep ?? auction.BidStep;
        var newReserve = request.ClearReservePrice ? null : request.ReservePrice ?? auction.ReservePrice;
        var newBuyNow = request.ClearBuyNowPrice ? null : request.BuyNowPrice ?? auction.BuyNowPrice;
        var newStartAt = request.StartAt ?? auction.StartAt;
        var newEndAt = request.EndAt ?? auction.EndAt;

        if (newEndAt <= newStartAt)
        {
            return (false, null, ["Thời điểm kết thúc phải sau thời điểm bắt đầu."]);
        }

        if (newReserve.HasValue && newReserve.Value < newStartPrice)
        {
            return (false, null, ["Giá sàn không được thấp hơn giá khởi điểm."]);
        }

        if (newBuyNow.HasValue && newBuyNow.Value <= newStartPrice)
        {
            return (false, null, ["Giá mua ngay phải cao hơn giá khởi điểm."]);
        }

        auction.StartPrice = newStartPrice;
        auction.BidStep = newBidStep;
        auction.ReservePrice = newReserve;
        auction.BuyNowPrice = newBuyNow;
        auction.StartAt = newStartAt;
        auction.EndAt = newEndAt;

        // Chưa có bid ⇒ CurrentPrice luôn bám theo giá khởi điểm.
        auction.CurrentPrice = newStartPrice;

        // Giữ AuctionType khớp với việc có/không có giá mua ngay.
        auction.AuctionType = newBuyNow.HasValue ? AuctionType.BuyNow : AuctionType.Standard;

        // Mở ngay nếu người bán dời StartAt về quá khứ.
        if (auction.Status == AuctionStatus.Scheduled && newStartAt <= now)
        {
            auction.Status = AuctionStatus.Active;
        }

        auction.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        return (true, AuctionMapper.ToDto(auction, null), []);
    }
}
