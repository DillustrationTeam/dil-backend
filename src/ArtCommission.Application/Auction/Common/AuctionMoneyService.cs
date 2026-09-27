using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Auction.Common;

/// <summary>
/// Nghiệp vụ khoá / nhả tiền cọc đấu giá.
///
/// VÌ SAO phải có lớp này thay vì gọi thẳng <see cref="IWalletService"/> ở từng handler:
/// mỗi lượt bid làm đổi trạng thái CỦA HAI người (người bị đè giá và người đặt mới).
/// Nếu mỗi handler tự ghép 2 thao tác đó, sớm muộn sẽ có chỗ quên nhả tiền của người
/// bị đè — và tiền đó nằm chết trong LockedBalance, không ai phát hiện.
///
/// Bất biến mà lớp này bảo đảm:
///   1. Mọi lượt bid KHÔNG còn Leading đều phải có <c>HoldStatus</c> là Refunded hoặc
///      Released, không bao giờ còn Held.
///   2. <c>RefId</c> của mọi dòng sổ cái là <c>Bid.Id</c> — không phải Auction.Id.
///      Xem <see cref="AuctionRefTypes"/> để hiểu vì sao điều này bắt buộc: dùng
///      Auction.Id sẽ vi phạm unique index ngay ở lượt bid thứ hai của cùng phiên.
/// </summary>
public class AuctionMoneyService : IAuctionMoneyService
{
    private readonly IWalletService _walletService;

    public AuctionMoneyService(IWalletService walletService)
    {
        _walletService = walletService;
    }

    public async Task<(AuctionHoldResult Result, WalletTransaction? Ledger)> HoldDepositAsync(
        Guid userId,
        Guid bidId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var wallet = await _walletService.GetOrCreateWalletAsync(userId, cancellationToken);

        if (wallet.Status != WalletStatus.Active)
        {
            return (AuctionHoldResult.NoWallet(
                "Ví đang bị khoá nên không thể đặt giá."), null);
        }

        if (wallet.Balance < amount)
        {
            return (AuctionHoldResult.Insufficient(
                wallet.Balance, $"Số dư khả dụng không đủ để khoá cọc {amount:N0} VND."), null);
        }

        var ledger = await _walletService.HoldFundsAsync(
            wallet,
            WalletTransactionType.EscrowHold,
            amount,
            AuctionRefTypes.BidDeposit,
            bidId,
            $"Khoá tiền cọc cho lượt đặt giá {bidId}",
            cancellationToken);

        return (
            AuctionHoldResult.Ok(wallet.Balance, wallet.LockedBalance),
            ledger);
    }

    public async Task<WalletTransaction?> RefundDepositAsync(
        Bid bid,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (bid.HoldStatus != HoldStatus.Held || bid.HoldAmount <= 0m)
        {
            // Đã nhả trước đó — bảo đảm hàm này idempotent.
            return null;
        }

        var wallet = await _walletService.FindWalletAsync(bid.BidderId, cancellationToken);
        if (wallet is null)
        {
            return null;
        }

        var ledger = await _walletService.RefundHeldFundsAsync(
            wallet,
            WalletTransactionType.RefundFromHold,
            bid.HoldAmount,
            AuctionRefTypes.BidDeposit,
            bid.Id,
            reason,
            cancellationToken);

        bid.HoldStatus = HoldStatus.Refunded;
        return ledger;
    }

    public async Task<WalletTransaction?> ReleaseDepositAsync(
        Bid bid,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (bid.HoldStatus != HoldStatus.Held || bid.HoldAmount <= 0m)
        {
            return null;
        }

        var wallet = await _walletService.FindWalletAsync(bid.BidderId, cancellationToken);
        if (wallet is null)
        {
            return null;
        }

        // Tiền cọc RỜI khỏi ví để trả cho người bán (không quay lại số dư khả dụng).
        var ledger = await _walletService.ReleaseFundsAsync(
            wallet,
            WalletTransactionType.EscrowRelease,
            bid.HoldAmount,
            AuctionRefTypes.BidDeposit,
            bid.Id,
            reason,
            cancellationToken);

        bid.HoldStatus = HoldStatus.Released;
        return ledger;
    }
}

/// <summary>Kết quả một lần giữ tiền cọc.</summary>
public sealed record AuctionHoldResult(
    bool Success,
    decimal Balance,
    decimal LockedBalance,
    string[] Errors)
{
    public static AuctionHoldResult Ok(decimal balance, decimal lockedBalance) =>
        new(true, balance, lockedBalance, []);

    public static AuctionHoldResult Insufficient(decimal balance, string message) =>
        new(false, balance, 0m, [message]);

    public static AuctionHoldResult NoWallet(string message) =>
        new(false, 0m, 0m, [message]);
}

/// <summary>Cổng nghiệp vụ cọc đấu giá — handler inject interface này, không inject lớp cụ thể.</summary>
public interface IAuctionMoneyService
{
    /// <param name="bidId">
    /// Id của lượt bid sẽ sở hữu khoản cọc này. Dùng làm <c>RefId</c> của dòng sổ cái.
    /// Bắt buộc phải là Bid.Id: dùng Auction.Id sẽ vi phạm unique index
    /// <c>UX_WalletTransaction_Ref</c> ngay ở lượt bid thứ hai của cùng phiên.
    /// </param>
    Task<(AuctionHoldResult Result, WalletTransaction? Ledger)> HoldDepositAsync(
        Guid userId,
        Guid bidId,
        decimal amount,
        CancellationToken cancellationToken = default);

    Task<WalletTransaction?> RefundDepositAsync(
        Bid bid,
        string reason,
        CancellationToken cancellationToken = default);

    Task<WalletTransaction?> ReleaseDepositAsync(
        Bid bid,
        string reason,
        CancellationToken cancellationToken = default);
}
