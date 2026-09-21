using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Commands.BuyNow;

/// <summary>
/// UC33 — POST /api/v1/auctions/{auctionId}/buy-now
/// Người mua chấp nhận <c>buyNowPrice</c> để kết thúc phiên ngay.
///
/// Khác luồng đặt giá ở chỗ tiền KHÔNG được giữ trước: người mua bị trừ trực tiếp
/// số dư khả dụng. Vì vậy phải kiểm tra đủ số dư trước khi đụng tới trạng thái phiên,
/// nếu không phiên bị đóng mà không có ai trả tiền.
/// </summary>
public record BuyNowCommand(
    Guid UserId,
    Guid AuctionId
) : IRequest<(bool Success, BuyNowResultDto? Data, string[] Errors)>;

public class BuyNowCommandHandler
    : IRequestHandler<BuyNowCommand, (bool Success, BuyNowResultDto?, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly IAuctionSettlementService _settlementService;

    public BuyNowCommandHandler(
        IApplicationDbContext db,
        IAuctionSettlementService settlementService)
    {
        _db = db;
        _settlementService = settlementService;
    }

    public async Task<(bool, BuyNowResultDto?, string[])> Handle(
        BuyNowCommand request,
        CancellationToken cancellationToken)
    {
        if (request.AuctionId == Guid.Empty)
        {
            return (false, null, ["Thiếu mã phiên đấu giá."]);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var auction = await _db.Auctions.FirstOrDefaultAsync(
            a => a.Id == request.AuctionId && !a.IsDeleted,
            cancellationToken);

        if (auction is null)
        {
            return (false, null, ["Không tìm thấy phiên đấu giá."]);
        }

        var now = DateTimeOffset.UtcNow;

        if (auction.Status == AuctionStatus.Active && auction.EndAt <= now)
        {
            auction.Status = AuctionStatus.Ended;
            auction.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
        }

        if (auction.Status != AuctionStatus.Active)
        {
            return (false, null,
                [$"Phiên không còn nhận mua ngay (trạng thái: {AuctionMapper.DescribeStatus(auction.Status)})."]);
        }

        if (!auction.BuyNowPrice.HasValue || auction.BuyNowPrice.Value <= 0m)
        {
            return (false, null, ["Phiên này không bật chức năng mua ngay."]);
        }

        if (auction.SellerId == request.UserId)
        {
            return (false, null, ["Người bán không thể tự mua ngay phiên của mình."]);
        }

        var (success, result, errors) = await _settlementService.SettleBuyNowAsync(
            auction,
            request.UserId,
            auction.BuyNowPrice.Value,
            cancellationToken);

        if (!success || result is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return (false, null, errors);
        }

        await tx.CommitAsync(cancellationToken);

        var auctionDto = AuctionMapper.ToDto(auction, null);

        return (true, new BuyNowResultDto(auctionDto, null, auction.PaymentDeadline), []);
    }
}
