using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Queries.GetAuctionSettlement;

/// <summary>
/// UC35 — GET /api/v1/auctions/{auctionId}/settlement
/// Winner xem kết quả chốt phiên và trạng thái thanh toán của mình.
///
/// Chỉ winner và seller xem được: kết quả giao dịch của người khác là dữ liệu riêng tư.
/// </summary>
public record GetAuctionSettlementQuery(
    Guid AuctionId,
    Guid CurrentUserId
) : IRequest<(bool Success, AuctionSettlementStatusDto? Data, string[] Errors)>;

public class GetAuctionSettlementQueryHandler
    : IRequestHandler<GetAuctionSettlementQuery, (bool, AuctionSettlementStatusDto?, string[])>
{
    private readonly IApplicationDbContext _db;

    public GetAuctionSettlementQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, AuctionSettlementStatusDto?, string[])> Handle(
        GetAuctionSettlementQuery request,
        CancellationToken cancellationToken)
    {
        if (request.AuctionId == Guid.Empty)
        {
            return (false, null, ["Thiếu mã phiên đấu giá."]);
        }

        var auction = await _db.Auctions
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AuctionId && !a.IsDeleted, cancellationToken);

        if (auction is null)
        {
            return (false, null, ["Không tìm thấy phiên đấu giá."]);
        }

        if (auction.Status is not (AuctionStatus.Settled or AuctionStatus.Expired))
        {
            return (false, null, ["Phiên chưa được chốt nên chưa có kết quả thanh toán."]);
        }

        if (request.CurrentUserId != auction.SellerId
            && request.CurrentUserId != auction.WinnerId)
        {
            return (false, null, ["Bạn không có quyền xem kết quả của phiên này."]);
        }

        var ownership = await _db.ArtworkOwnerships
            .AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.AuctionId == auction.Id && o.IsCurrent && !o.IsDeleted,
                cancellationToken);

        var escrow = await _db.EscrowTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.AuctionId == auction.Id && !e.IsDeleted, cancellationToken);

        var paymentOrderStatus = ResolvePaymentStatus(auction, escrow);

        var dto = new AuctionSettlementStatusDto(
            AuctionId: auction.Id,
            WinnerId: auction.WinnerId,
            FinalPrice: auction.FinalPrice ?? 0m,
            SettledAt: auction.SettledAt,
            PaymentOrderStatus: paymentOrderStatus,
            ArtworkOwnershipId: ownership?.Id,
            PaymentDeadline: auction.PaymentDeadline,
            EscrowHeldAmount: escrow is null
                ? null
                : escrow.Amount - escrow.ReleasedAmount - escrow.RefundedAmount);

        return (true, dto, []);
    }

    /// <summary>
    /// Suy ra trạng thái thanh toán của winner.
    ///
    /// Trong luồng đấu giá, tiền được trả bằng số dư ví nên KHÔNG sinh PaymentOrder
    /// (PaymentOrder dành cho luồng nạp tiền). Vì vậy ở đây suy từ trạng thái escrow.
    /// </summary>
    private static string? ResolvePaymentStatus(
        Domain.Entities.Auction.Auction auction,
        Domain.Entities.Auction.EscrowTransaction? escrow)
    {
        if (auction.Status == AuctionStatus.Expired)
        {
            return "Expired";
        }

        if (escrow is null)
        {
            return auction.SettledAt.HasValue ? "Pending" : null;
        }

        return escrow.Status switch
        {
            EscrowStatus.Released => "Paid",
            EscrowStatus.PartialReleased => "PartiallyPaid",
            EscrowStatus.Refunded => "Refunded",
            EscrowStatus.Disputed => "Disputed",
            EscrowStatus.Deposited => "Held",
            _ => "Pending"
        };
    }
}
