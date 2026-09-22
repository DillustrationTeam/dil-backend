namespace ArtCommission.Application.Wallets.Common;

/// <summary>Ví của người dùng (UC47).</summary>
public record WalletDto(
    Guid WalletId,
    decimal Balance,
    decimal LockedBalance,
    string Currency,
    string WalletStatus
);

/// <summary>Bốn chỉ số tổng quan hiển thị trên trang /wallet (UC47).</summary>
/// <param name="EscrowHeldAmount">Tiền đang bị giữ (escrow đơn vẽ + cọc đấu giá) — lấy từ Wallet.LockedBalance.</param>
/// <param name="TotalWithdrawn">Tổng tiền đã rút thành công — tính từ sổ cái, chỉ gồm payout đã duyệt.</param>
/// <param name="PendingPayoutAmount">
/// Tiền đang CHỜ RÚT: đã trừ khỏi ví nhưng Admin chưa chuyển khoản (PayoutRequest.Status = Pending).
/// Tách khỏi <paramref name="TotalWithdrawn"/> vì tiền chờ rút vẫn có thể bị từ chối và hoàn lại ví.
/// </param>
public record WalletSummaryDto(
    decimal EscrowHeldAmount,
    decimal TotalWithdrawn,
    decimal PendingPayoutAmount
);

/// <summary>Kết quả GET /api/v1/wallets/me (UC47).</summary>
public record WalletOverviewDto(WalletDto Wallet, WalletSummaryDto Summary);

/// <summary>Một dòng biến động số dư trong sổ cái (UC47).</summary>
public record WalletTransactionDto(
    Guid WalletTransactionId,
    string WalletTxType,
    /// <summary>"In" hoặc "Out".</summary>
    string Direction,
    decimal Amount,
    decimal BalanceAfter,
    string? RefType,
    Guid? RefId,
    DateTimeOffset CreatedAt,
    string? Note
);
