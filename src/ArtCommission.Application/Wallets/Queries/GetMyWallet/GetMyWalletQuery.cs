using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Wallets.Common;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Wallets.Queries.GetMyWallet;

/// <summary>
/// UC47 — GET /api/v1/wallets/me
/// Người dùng mở trang /wallet xem 3 thẻ chỉ số:
/// số dư khả dụng, tiền đang giữ escrow, tổng đã rút.
/// </summary>
public record GetMyWalletQuery(Guid UserId)
    : IRequest<(bool Success, WalletOverviewDto? Data, string[] Errors)>;

public class GetMyWalletQueryHandler
    : IRequestHandler<GetMyWalletQuery, (bool, WalletOverviewDto?, string[])>
{
    private readonly IApplicationDbContext _db;

    public GetMyWalletQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, WalletOverviewDto?, string[])> Handle(
        GetMyWalletQuery request,
        CancellationToken cancellationToken)
    {
        var wallet = await _db.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == request.UserId && !w.IsDeleted, cancellationToken);

        // Người dùng chưa từng giao dịch thì chưa có ví — trả ví rỗng thay vì 404
        // để trang /wallet hiển thị số 0 thay vì báo lỗi.
        if (wallet is null)
        {
            return (true, new WalletOverviewDto(
                Wallet: new WalletDto(Guid.Empty, 0m, 0m, "VND", WalletStatusNames.Active),
                Summary: new WalletSummaryDto(0m, 0m)
            ), []);
        }

        // Tổng đã rút: CHỈ tính yêu cầu rút đã được Admin duyệt (Processed).
        // - Pending: tiền đã trừ khỏi ví nhưng chưa chắc chuyển khoản thành công
        // - Rejected: Admin từ chối, tiền đã hoàn lại ví -> không tính là đã rút
        // Nên phải join sang PayoutRequest để lọc theo trạng thái, không sum cả sổ cái.
        var totalWithdrawn = await _db.WalletTransactions
            .AsNoTracking()
            .Where(t => t.WalletId == wallet.Id
                        && t.Type == WalletTransactionType.Payout
                        && !t.IsDeleted
                        && t.RefId != null
                        && _db.PayoutRequests.Any(p =>
                            p.Id == t.RefId.Value && p.Status == PayoutStatus.Processed))
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        // Tiền đang giữ escrow: Wallet.LockedBalance là nguồn duy nhất.
        // Module Commission/Auction ghi vào đây qua IWalletService.HoldFundsAsync.
        var escrowHeld = wallet.LockedBalance;

        return (true, new WalletOverviewDto(
            Wallet: new WalletDto(
                WalletId: wallet.Id,
                Balance: wallet.Balance,
                LockedBalance: wallet.LockedBalance,
                Currency: wallet.Currency,
                WalletStatus: wallet.Status.ToString()
            ),
            Summary: new WalletSummaryDto(escrowHeld, totalWithdrawn)
        ), []);
    }
}
